using System.Collections;
using System.Reflection;
using System.Threading.RateLimiting;
using ClinicLive.Api;
using ClinicLive.Contracts;
using ClinicLive.Domain;
using ClinicLive.Services.Ai;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ClinicLive.Tests;

/// <summary>
/// Part 9's promise, against the real pgvector container: the patient assistant cannot answer
/// from a staff document, and cannot be talked into it either, because the question never
/// reaches the model at all.
/// <para>
/// The proof is arranged so there is nowhere to hide. With <see cref="FakeEmbeddingGenerator"/>
/// a chunk's own text embeds to that chunk's own vector, so asking a staff chunk's exact words
/// makes that chunk the nearest thing in the corpus by a mile — distance 0 against roughly 1
/// for every public chunk. If visibility were a filter applied after the search, or a sentence
/// in a prompt, this question is precisely the one that would leak.
/// </para>
/// </summary>
[Collection("postgres")]
public class PatientAssistantTests(PostgresFixture fx) : IAsyncLifetime
{
    private static readonly AiOptions Options =
        new("http://127.0.0.1:11434", "qwen2.5:14b", "nomic-embed-text", "24h", 6, 0.55);

    public async Task InitializeAsync()
    {
        await ClearKnowledgeAsync();

        var source = FindKnowledgeFolder();
        await new KnowledgeIngester(
            fx.DbFactory,
            new FakeEmbeddingGenerator(),
            new ConfigurationBuilder().Build(),
            new TestHostEnvironment(source),
            NullLogger<KnowledgeIngester>.Instance).IngestAsync(source);
    }

    public Task DisposeAsync() => ClearKnowledgeAsync();

    [Fact]
    public async Task A_public_question_whose_nearest_chunk_is_staff_only_gets_the_refusal()
    {
        await using var db = await fx.DbFactory.CreateDbContextAsync();

        // The clinic's internal rate card: the one document a patient must never be read.
        var secret = await db.KnowledgeChunks
            .Include(c => c.Document)
            .FirstAsync(c => c.Document.Audience == KnowledgeAudience.Staff
                          && c.Text.Contains("Home sample collection"));

        var chat = new FakeChatClient("Home sample collection is charged at the internal rate.");

        // Ask with the staff chunk's own words, as the public.
        var reply = await NewService(chat).AskAsync(secret.Text, KnowledgeAudience.Public);
        var deltas = await CollectAsync(reply.Deltas);

        // Nothing was retrieved, so nothing was prompted with, so the model was never called:
        // the leak cannot happen in a place no request was made from.
        Assert.Empty(reply.Chunks);
        Assert.Equal(0, chat.Calls);
        Assert.Equal([AssistantService.RefusalText], deltas);
        Assert.Equal(AssistantContract.Refusal, AssistantContract.NormalizeAnswer(string.Concat(deltas)));

        // And the same words asked by staff DO find it — otherwise the test above would pass
        // just as happily against an empty database.
        var staffHits = await NewRetriever().SearchAsync(secret.Text, KnowledgeAudience.Staff, 6);
        Assert.Equal(secret.Id, staffHits[0].ChunkId);
        Assert.True(staffHits[0].Distance < 1e-5, $"identical text should be distance 0, was {staffHits[0].Distance}");

        // The public search is not empty — it simply cannot see that row.
        var publicHits = await NewRetriever().SearchAsync(secret.Text, KnowledgeAudience.Public, 6);
        Assert.NotEmpty(publicHits);
        Assert.DoesNotContain(publicHits, h => h.ChunkId == secret.Id);
        Assert.All(publicHits, h => Assert.True(h.Distance > Options.MaxDistance));
    }

    [Fact]
    public async Task A_public_question_the_public_documents_answer_still_reaches_the_model()
    {
        // The other half of the proof: "public only" is a filter, not a gag.
        var question = await PublicChunkTextAsync();

        var chat = new FakeChatClient("Bring your confirmation code.\nSources: [1]");
        var reply = await NewService(chat).AskAsync(question, KnowledgeAudience.Public);
        var deltas = await CollectAsync(reply.Deltas);

        Assert.NotEmpty(reply.Chunks);
        Assert.Equal(1, chat.Calls);
        Assert.DoesNotContain(AssistantService.GuardMarker, deltas);

        // No tool was offered on the public path — that is Part 8's guard, restated here
        // because Part 9 is the audience that must never get one.
        Assert.True(chat.LastOptions?.Tools is null or { Count: 0 });

        await using var db = await fx.DbFactory.CreateDbContextAsync();
        var publicSlugs = await db.KnowledgeDocuments
            .Where(d => d.Audience == KnowledgeAudience.Public)
            .Select(d => d.Slug)
            .ToListAsync();

        Assert.All(reply.Chunks, c => Assert.Contains(c.DocumentSlug, publicSlugs));
    }

    [Fact]
    public async Task Even_a_queue_question_gets_no_tool_from_the_public()
    {
        // "Who is next?" is a queue question by the classifier's own words, and the queue is
        // not public. The audience test in AssistantService is what stops it, not the wording.
        var chat = new FakeChatClient(null);
        var reply = await NewService(chat).AskAsync("Who is next in the queue right now?", KnowledgeAudience.Public);
        var deltas = await CollectAsync(reply.Deltas);

        Assert.True(AssistantService.IsQueueQuestion("Who is next in the queue right now?"));
        Assert.Equal([AssistantService.RefusalText], deltas);
        Assert.Equal(0, chat.Calls);
    }

    private KnowledgeRetriever NewRetriever() =>
        new(fx.DbFactory, new FakeEmbeddingGenerator(), NullLogger<KnowledgeRetriever>.Instance);

    private AssistantService NewService(FakeChatClient chat) =>
        new(chat,
            NewRetriever(),
            new AssistantTools(
                new ClinicLive.Services.QueueService(fx.DbFactory, new FakeQueueHub(), fx.ClinicTime, new FakePushSender()),
                NullLogger<AssistantTools>.Instance),
            Options,
            new ConfigurationBuilder().Build(),
            NullLogger<AssistantService>.Instance);

    private async Task<string> PublicChunkTextAsync()
    {
        await using var db = await fx.DbFactory.CreateDbContextAsync();
        return await db.KnowledgeChunks
            .Where(c => c.Document.Audience == KnowledgeAudience.Public)
            .OrderBy(c => c.Id)
            .Select(c => c.Text)
            .FirstAsync();
    }

    private async Task ClearKnowledgeAsync()
    {
        await using var db = await fx.DbFactory.CreateDbContextAsync();
        await db.KnowledgeChunks.ExecuteDeleteAsync();
        await db.KnowledgeDocuments.ExecuteDeleteAsync();
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

    private static string FindKnowledgeFolder()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "knowledge");
            if (Directory.Exists(candidate)) return candidate;
        }

        throw new DirectoryNotFoundException("No knowledge folder above " + AppContext.BaseDirectory);
    }

    private sealed class TestHostEnvironment(string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "ClinicLive.Tests";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

/// <summary>
/// The endpoint's two gates, tested where they live. There is no WebApplicationFactory in this
/// suite (the app boots Identity, SignalR and a real database), so these test the validation
/// helper the endpoint calls and the limiter options the endpoint requires by name — the same
/// objects, one frame below the HTTP.
/// </summary>
public class AssistantEndpointTests
{
    [Theory]
    [InlineData("ab")]                       // 2 characters: the short end of the gate
    [InlineData("  a  ")]                    // 1 character once trimmed — spaces are not a question
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void A_question_that_is_too_short_is_rejected(string? question) =>
        Assert.False(AssistantContract.IsAskable(question));

    [Fact]
    public void A_question_of_301_characters_is_rejected() =>
        Assert.False(AssistantContract.IsAskable(new string('a', 301)));

    [Theory]
    [InlineData(3)]
    [InlineData(300)]
    public void The_edges_of_the_allowed_length_are_accepted(int length) =>
        Assert.True(AssistantContract.IsAskable(new string('a', length)));

    [Fact]
    public void A_real_question_is_accepted() =>
        Assert.True(AssistantContract.IsAskable("What should I bring to my appointment?"));

    [Fact]
    public void The_assistant_policy_is_ten_questions_a_minute()
    {
        var provider = new ServiceCollection()
            .AddLogging()
            .AddAssistantRateLimiter()
            .BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        Assert.Equal(StatusCodes.Status429TooManyRequests, options.RejectionStatusCode);
        Assert.True(
            RegisteredPolicyNames(options).Contains(PocketEndpoints.AssistantPolicy),
            $"no rate-limiter policy named '{PocketEndpoints.AssistantPolicy}' was registered");

        var limits = PocketEndpoints.AssistantLimiterOptions();
        Assert.Equal(10, limits.PermitLimit);
        Assert.Equal(TimeSpan.FromMinutes(1), limits.Window);
        Assert.Equal(0, limits.QueueLimit);          // over the limit is a 429 now, not a wait
        Assert.Equal(PocketEndpoints.AssistantPermitLimit, limits.PermitLimit);
        Assert.Equal(PocketEndpoints.AssistantWindow, limits.Window);
    }

    [Fact]
    public void The_eleventh_question_in_a_window_is_refused()
    {
        // The numbers above, as behaviour: ten leases, then no more until the window turns.
        using var limiter = new FixedWindowRateLimiter(PocketEndpoints.AssistantLimiterOptions());

        var leases = new List<RateLimitLease>();
        for (var i = 0; i < PocketEndpoints.AssistantPermitLimit; i++)
        {
            var lease = limiter.AttemptAcquire();
            Assert.True(lease.IsAcquired, $"question {i + 1} of {PocketEndpoints.AssistantPermitLimit} should have been allowed");
            leases.Add(lease);
        }

        Assert.False(limiter.AttemptAcquire().IsAcquired);

        foreach (var lease in leases)
        {
            lease.Dispose();
        }
    }

    [Fact]
    public void The_refusal_the_app_knows_is_the_refusal_the_server_sends()
    {
        // Two assemblies, one sentence. The app cannot reference AssistantService, so this is
        // the only thing standing between "I don't know that" and a silent drift.
        Assert.Equal(AssistantService.RefusalText, AssistantContract.Refusal);
        Assert.True(AssistantContract.IsRefusal(AssistantService.RefusalText));

        // And the guard's shape on the wire: a withdrawn answer ends with that sentence, and
        // all the client may show is the sentence.
        Assert.Equal(
            AssistantContract.Refusal,
            AssistantContract.NormalizeAnswer("Parking is free all day. " + AssistantContract.Refusal));
    }

    [Fact]
    public void Citations_follow_the_numbers_the_answer_named()
    {
        const string header = "[1] Patient information > What to bring | [2] Fees and payment | [3] Parking";

        Assert.Equal(
            ["Fees and payment"],
            AssistantContract.Citations("Cash or card.\nSources: [2]", header));

        // No usable number: the nearest document is still the honest provenance.
        Assert.Equal(
            ["Patient information > What to bring"],
            AssistantContract.Citations("Bring your code.", header));

        // A refusal stands on nothing, and says so.
        Assert.Empty(AssistantContract.Citations(AssistantContract.Refusal, header));

        // Including the shape the live kiosk produced on its first run: the refusal with a
        // Sources line stapled underneath it. Either a citation or the refusal, never both.
        Assert.Empty(AssistantContract.Citations(
            AssistantContract.Refusal + "\nSources: [1], [2]", header));

        // And a withdrawn answer (the guard's last delta) cites nothing either.
        Assert.Empty(AssistantContract.Citations(
            "Parking is free all day. " + AssistantContract.Refusal, header));

        // The line itself is not what a patient reads.
        Assert.Equal("Cash or card.", AssistantContract.WithoutSourcesLine("Cash or card.\nSources: [2]"));
    }

    /// <summary>
    /// The names in the options' policy map. ASP.NET Core keeps that map internal, so this
    /// reads it reflectively rather than re-implementing the middleware to observe it.
    /// </summary>
    private static IReadOnlyList<string> RegisteredPolicyNames(RateLimiterOptions options)
    {
        var names = new List<string>();

        foreach (var property in typeof(RateLimiterOptions)
                     .GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (property.GetIndexParameters().Length > 0) continue;
            if (property.GetValue(options) is not IDictionary map) continue;

            foreach (var key in map.Keys)
            {
                if (key is string name)
                {
                    names.Add(name);
                }
            }
        }

        return names;
    }
}
