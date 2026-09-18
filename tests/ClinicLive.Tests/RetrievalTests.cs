using ClinicLive.Domain;
using ClinicLive.Services.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClinicLive.Tests;

/// <summary>
/// Part 7's three promises, against the real pgvector container: a staff chunk is invisible to
/// the public assistant because the SQL cannot return it; an answer without a citation is
/// replaced by the refusal; and a question nothing in the corpus answers never reaches the model.
/// <para>
/// The embedder is <see cref="FakeEmbeddingGenerator"/>, so no test needs Ollama. It is
/// deterministic, which buys the whole suite its arithmetic: embedding a chunk's own text gives
/// back that chunk's stored vector, so the distance is 0, and embedding anything else gives a
/// vector near-orthogonal to all 768 dimensions of every chunk, so the distance is about 1.
/// </para>
/// </summary>
[Collection("postgres")]
public class RetrievalTests(PostgresFixture fx) : IAsyncLifetime
{
    private static readonly AiOptions Options =
        new("http://127.0.0.1:11434", "qwen2.5:14b", "nomic-embed-text", "24h", 6, 0.55);

    private readonly FakeEmbeddingGenerator _embeddings = new();

    /// <summary>Load the clinic's documents fresh; the container is shared with the ingest tests.</summary>
    public async Task InitializeAsync()
    {
        await ClearKnowledgeAsync();

        var source = FindKnowledgeFolder();
        var ingester = new KnowledgeIngester(
            fx.DbFactory,
            new FakeEmbeddingGenerator(),
            new ConfigurationBuilder().Build(),
            new TestHostEnvironment(source),
            NullLogger<KnowledgeIngester>.Instance);

        await ingester.IngestAsync(source);
    }

    /// <summary>Leave the database as we found it — the ingest test expects to load it itself.</summary>
    public Task DisposeAsync() => ClearKnowledgeAsync();

    [Fact]
    public async Task Staff_chunk_is_found_for_staff_and_is_invisible_to_the_public()
    {
        await using var db = await fx.DbFactory.CreateDbContextAsync();

        var secret = await db.KnowledgeChunks
            .Include(c => c.Document)
            .FirstAsync(c => c.Document.Audience == KnowledgeAudience.Staff
                          && c.Text.Contains("Home sample collection"));

        var retriever = NewRetriever();

        // Asking with the chunk's own words: the fake embedder returns the stored vector,
        // so this chunk sits at distance 0 and must be the first row back.
        var staffHits = await retriever.SearchAsync(secret.Text, KnowledgeAudience.Staff, 6);

        Assert.NotEmpty(staffHits);
        Assert.Equal(secret.Id, staffHits[0].ChunkId);
        Assert.True(staffHits[0].Distance < 1e-5, $"identical text should be distance 0, was {staffHits[0].Distance}");
        Assert.Equal("Suppliers and internal rates", staffHits[0].DocumentTitle);

        // Same question, public audience: the row the query can return does not include it.
        var publicHits = await retriever.SearchAsync(secret.Text, KnowledgeAudience.Public, 6);

        Assert.DoesNotContain(publicHits, h => h.ChunkId == secret.Id);

        var publicSlugs = await db.KnowledgeDocuments
            .Where(d => d.Audience == KnowledgeAudience.Public)
            .Select(d => d.Slug)
            .ToListAsync();

        Assert.NotEmpty(publicHits);
        Assert.All(publicHits, h => Assert.Contains(h.DocumentSlug, publicSlugs));

        // And an unknown audience string fails closed rather than opening the doors.
        var strangerHits = await retriever.SearchAsync(secret.Text, "everyone", 6);
        Assert.DoesNotContain(strangerHits, h => h.ChunkId == secret.Id);
    }

    [Fact]
    public async Task An_answer_without_a_citation_becomes_the_refusal()
    {
        var question = await ChunkTextAsync(KnowledgeAudience.Public);

        // A plausible, fluent, completely uncited answer — the failure mode the guard exists for.
        var chat = new FakeChatClient("Bring your appointment code and a photo ID.");
        var reply = await NewService(chat).AskAsync(question, KnowledgeAudience.Staff);
        var deltas = await CollectAsync(reply.Deltas);

        Assert.NotEmpty(reply.Chunks);
        Assert.Equal(1, chat.Calls);

        // The prompt carried the documents...
        var prompt = chat.LastMessages[^1].Text ?? string.Empty;
        Assert.Contains("CLINIC DOCUMENTS:", prompt, StringComparison.Ordinal);
        Assert.Contains("QUESTION: ", prompt, StringComparison.Ordinal);

        // ...and the stream ends with the marker that tells the page to show the refusal.
        Assert.Equal(AssistantService.GuardMarker, deltas[^1]);

        var streamed = string.Concat(deltas[..^1]);
        Assert.False(AssistantService.HasCitation(streamed));
        Assert.False(AssistantService.IsRefusal(streamed));
    }

    [Fact]
    public async Task A_question_nothing_answers_refuses_without_calling_the_model()
    {
        // Nothing in a clinic's documents is near this, so every distance lands around 1.0.
        var chat = new FakeChatClient(null);
        var reply = await NewService(chat).AskAsync("Which varnish suits a trombone case?", KnowledgeAudience.Staff);
        var deltas = await CollectAsync(reply.Deltas);

        Assert.Equal([AssistantService.RefusalText], deltas);
        Assert.Empty(reply.Chunks);
        Assert.Equal(0, chat.Calls);
    }

    [Fact]
    public async Task A_cited_answer_keeps_its_citation_numbers()
    {
        var question = await ChunkTextAsync(KnowledgeAudience.Public);

        var chat = new FakeChatClient("Arrive ten minutes early with your code.\nSources: [1], [3]");
        var reply = await NewService(chat).AskAsync(question, KnowledgeAudience.Staff);
        var deltas = await CollectAsync(reply.Deltas);

        Assert.DoesNotContain(AssistantService.GuardMarker, deltas);
        Assert.Equal([1, 3], AssistantService.CitedNumbers(string.Concat(deltas)));
    }

    private KnowledgeRetriever NewRetriever() =>
        new(fx.DbFactory, _embeddings, NullLogger<KnowledgeRetriever>.Instance);

    private AssistantService NewService(FakeChatClient chat) =>
        new(chat,
            NewRetriever(),
            Options,
            new ConfigurationBuilder().Build(),
            NullLogger<AssistantService>.Instance);

    /// <summary>The exact text of one stored chunk — a question guaranteed to retrieve something.</summary>
    private async Task<string> ChunkTextAsync(string audience)
    {
        await using var db = await fx.DbFactory.CreateDbContextAsync();
        return await db.KnowledgeChunks
            .Where(c => c.Document.Audience == audience)
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

    /// <summary>The documents live at the repository root; the test binary runs four folders down.</summary>
    private static string FindKnowledgeFolder()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "knowledge");
            if (Directory.Exists(candidate)) return candidate;
        }

        throw new DirectoryNotFoundException("No knowledge folder above " + AppContext.BaseDirectory);
    }

    /// <summary>Just enough IHostEnvironment for the ingester's folder walk.</summary>
    private sealed class TestHostEnvironment(string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "ClinicLive.Tests";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
