using System.Text.Json;
using ClinicLive.Domain;
using ClinicLive.Services;
using ClinicLive.Services.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Pgvector;

namespace ClinicLive.Tests;

/// <summary>
/// Part 8's classifier, on its own: no database, no model, no container. It decides whether the
/// request that leaves this machine carries a tools array at all, so it is worth a test that
/// runs in a millisecond and can never be flaky.
/// </summary>
public class QueueClassifierTests
{
    [Theory]
    [InlineData("Who has waited the longest right now?")]
    [InlineData("How many patients are waiting?")]
    [InlineData("Is there anyone in the queue?")]
    [InlineData("Who is next?")]
    [InlineData("Has Maria been called yet?")]
    public void A_queue_question_is_offered_the_tool(string question) =>
        Assert.True(AssistantService.IsQueueQuestion(question), question);

    [Theory]
    [InlineData("What are the opening hours?")]
    [InlineData("How much does a consultation cost?")]
    [InlineData("Where can patients park?")]
    [InlineData("What should I bring to my appointment?")]
    [InlineData("Do you offer home sample collection?")]
    public void Everything_else_is_asked_with_no_tools_at_all(string question) =>
        Assert.False(AssistantService.IsQueueQuestion(question), question);
}

/// <summary>
/// The tool itself, end to end: a model that asks for <c>get_queue</c>, the real function
/// invocation loop from Microsoft.Extensions.AI, the real <see cref="QueueService"/> over the
/// real pgvector container — and the two rules that matter. The function must run and its rows
/// must reach the answer; and the patient audience must never be handed the tool, no matter how
/// squarely its question lands on the queue.
/// </summary>
[Collection("postgres")]
public class ToolCallingTests(PostgresFixture fx) : IAsyncLifetime
{
    private const string TestDocumentSlug = "part8-queue-probe";

    private static readonly AiOptions Options =
        new("http://127.0.0.1:11434", "qwen2.5:14b", "nomic-embed-text", "24h", 6, 0.55);

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>Leave the knowledge table as it was found — the ingest tests count its rows.</summary>
    public async Task DisposeAsync()
    {
        await using var db = await fx.DbFactory.CreateDbContextAsync();
        await db.KnowledgeChunks.Where(c => c.Document.Slug == TestDocumentSlug).ExecuteDeleteAsync();
        await db.KnowledgeDocuments.Where(d => d.Slug == TestDocumentSlug).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task The_model_calls_get_queue_and_its_rows_reach_the_answer()
    {
        var queue = NewQueue();
        var (firstCode, secondCode) = await SeedTwoWaitingPatientsAsync(queue, hour: 11);

        // The model asks for the queue, then — once the JSON is back — writes the answer.
        var fake = new FakeChatClient(
            answer: "Priya N. has waited longest, 40 minutes. Sources: [queue]",
            callTool: AssistantTools.GetQueueName,
            callArguments: new Dictionary<string, object?> { ["status"] = "waiting" });

        // The wrapper Program.cs registers: this is what actually runs the function.
        var chat = new ChatClientBuilder(fake).UseFunctionInvocation().Build();

        var reply = await NewService(chat, queue).AskAsync(
            "Who has waited the longest right now?", KnowledgeAudience.Staff);
        var deltas = await CollectAsync(reply.Deltas);
        var answer = string.Concat(deltas);

        // Two round trips: the call, then the answer written over its result.
        Assert.Equal(2, fake.Calls);
        Assert.DoesNotContain(AssistantService.GuardMarker, deltas);

        // The model's thinking-out-loud was painted and then taken back: the page is told to
        // clear the bubble, and the answer the caller ends up with has no trace of it.
        Assert.Contains(AssistantService.ResetMarker, deltas);

        var kept = string.Concat(deltas.SkipWhile(d => d != AssistantService.ResetMarker).Skip(1));
        Assert.DoesNotContain(FakeChatClient.Preamble, kept, StringComparison.Ordinal);
        Assert.Contains("Priya N.", kept, StringComparison.Ordinal);
        Assert.Contains("[queue]", answer, StringComparison.Ordinal);
        Assert.True(AssistantService.CitesQueue(answer));

        // The tool was offered...
        var offered = Assert.Single(fake.LastOptions?.Tools ?? []);
        Assert.Equal(AssistantTools.GetQueueName, offered.Name);

        // ...and it really ran: the second request carries the function's own rows.
        var result = fake.LastMessages
            .SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>()
            .Single();
        var json = JsonSerializer.Serialize(result.Result);

        Assert.Contains("Priya N.", json, StringComparison.Ordinal);
        Assert.Contains("Tomas A.", json, StringComparison.Ordinal);
        Assert.Contains(firstCode, json, StringComparison.Ordinal);
        Assert.Contains(secondCode, json, StringComparison.Ordinal);

        // Masked, both ways: no surname, no phone number ever reaches the model.
        Assert.DoesNotContain("Priya Nair", json, StringComparison.Ordinal);
        Assert.DoesNotContain("+00-", json, StringComparison.Ordinal);

        // Longest first, and the minutes are the ones the rows were seeded with.
        var patients = JsonSerializer.Deserialize<List<QueuePatient>>(json)!;
        Assert.Equal("Priya N.", patients[0].Name);
        Assert.Equal(40, patients[0].MinutesWaited);
        Assert.Equal(25, patients.Single(p => p.Name == "Tomas A.").MinutesWaited);
        Assert.All(patients, p => Assert.Equal("waiting", p.Status));
    }

    /// <summary>
    /// The patient assistant asks the same question and is handed nothing. The classifier says
    /// yes — the words are all there — and the audience says no, which is the half of the guard
    /// that Part 9 will lean on when this pipeline is reachable from a kiosk.
    /// </summary>
    [Fact]
    public async Task The_patient_audience_is_never_offered_the_tool()
    {
        var queue = NewQueue();
        await SeedTwoWaitingPatientsAsync(queue, hour: 12);

        // A public chunk that IS the question, so retrieval is guaranteed to find something and
        // the model is actually reached — otherwise "no tools" would be true for a boring reason.
        const string question = "How many patients are waiting?";
        await SeedPublicChunkAsync(question);

        // Same wiring as the staff path — the difference has to come from the code, not the setup.
        var fake = new FakeChatClient("The waiting room is usually busiest after lunch. Sources: [1]");
        var chat = new ChatClientBuilder(fake).UseFunctionInvocation().Build();

        var reply = await NewService(chat, queue).AskAsync(question, KnowledgeAudience.Public);
        var deltas = await CollectAsync(reply.Deltas);

        Assert.True(AssistantService.IsQueueQuestion(question));   // the words are there
        Assert.Equal(1, fake.Calls);                               // and the model still answered once
        Assert.NotEmpty(reply.Chunks);

        // The whole point: nothing in the request the model could have called.
        Assert.True(fake.LastOptions?.Tools is null or { Count: 0 },
            $"the patient audience was offered {fake.LastOptions?.Tools?.Count} tool(s)");

        // So no function ever ran, and no queue row is anywhere near this conversation.
        Assert.Empty(fake.LastMessages.SelectMany(m => m.Contents).OfType<FunctionResultContent>());
        Assert.DoesNotContain("Priya", string.Concat(deltas), StringComparison.Ordinal);
    }

    private QueueService NewQueue() =>
        new(fx.DbFactory, new FakeQueueHub(), fx.ClinicTime, new FakePushSender());

    private AssistantService NewService(IChatClient chat, QueueService queue) =>
        new(chat,
            new KnowledgeRetriever(fx.DbFactory, new FakeEmbeddingGenerator(), NullLogger<KnowledgeRetriever>.Instance),
            new AssistantTools(queue, NullLogger<AssistantTools>.Instance),
            Options,
            new ConfigurationBuilder().Build(),
            NullLogger<AssistantService>.Instance);

    /// <summary>
    /// Two people in today's queue, checked in 40 and 25 minutes ago. The backdating is done in
    /// the database on purpose: "how long have they waited" is the one number this whole part is
    /// about, and a test that asserts "roughly zero minutes" would assert nothing.
    /// <para>
    /// Each test books its own hour: the container is shared with every other test in the
    /// "postgres" collection, and a slot is unique per day — two tests reaching for 15:45 is a
    /// booking clash, not a bug in the thing under test.
    /// </para>
    /// </summary>
    private async Task<(string First, string Second)> SeedTwoWaitingPatientsAsync(QueueService queue, int hour)
    {
        var booking = new BookingService(fx.DbFactory, fx.ClinicTime);
        var today = DateTime.UtcNow.Date;

        var priya = await booking.BookAsync("Priya Nair", "+00-1111-0801", null, today.AddHours(hour));
        var tomas = await booking.BookAsync("Tomas Alvarez", "+00-1111-0802", null, today.AddHours(hour).AddMinutes(15));

        Assert.True(priya.Success, priya.Error);
        Assert.True(tomas.Success, tomas.Error);

        Assert.True((await queue.CheckInAsync(priya.Appointment!.ConfirmationCode)).Success);
        Assert.True((await queue.CheckInAsync(tomas.Appointment!.ConfirmationCode)).Success);

        await using var db = await fx.DbFactory.CreateDbContextAsync();
        await BackdateAsync(db, priya.Appointment.Id, 40);
        await BackdateAsync(db, tomas.Appointment.Id, 25);

        return (priya.Appointment.ConfirmationCode, tomas.Appointment.ConfirmationCode);
    }

    private static Task BackdateAsync(ClinicLive.Data.ApplicationDbContext db, long appointmentId, int minutes) =>
        db.QueueEntries
            .Where(q => q.AppointmentId == appointmentId)
            .ExecuteUpdateAsync(s => s.SetProperty(q => q.CheckedInAt, DateTime.UtcNow.AddMinutes(-minutes)));

    /// <summary>One public chunk whose text is exactly <paramref name="text"/> — distance 0 to it.</summary>
    private async Task SeedPublicChunkAsync(string text)
    {
        await using var db = await fx.DbFactory.CreateDbContextAsync();
        if (await db.KnowledgeDocuments.AnyAsync(d => d.Slug == TestDocumentSlug))
        {
            return;
        }

        db.KnowledgeDocuments.Add(new KnowledgeDocument
        {
            Slug = TestDocumentSlug,
            Title = "Waiting times",
            Audience = KnowledgeAudience.Public,
            SourcePath = $"public/{TestDocumentSlug}.md",
            ContentHash = "part8",
            Chunks =
            [
                new KnowledgeChunk
                {
                    Ordinal = 0,
                    Heading = null,
                    Text = text,
                    TokenEstimate = 8,
                    Embedding = new Vector(FakeEmbeddingGenerator.Deterministic(text)),
                },
            ],
        });

        await db.SaveChangesAsync();
    }

    private static async Task<List<string>> CollectAsync(IAsyncEnumerable<string> deltas)
    {
        var all = new List<string>();
        await foreach (var delta in deltas)
        {
            all.Add(delta);
        }

        return all;
    }
}
