using ClinicLive.Domain;
using ClinicLive.Services.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClinicLive.Tests;

/// <summary>
/// Part 6 ends with the clinic's documents in PostgreSQL. This test proves it against a real
/// pgvector container — the vector(768) column, the cascade, and the skip-when-unchanged rule
/// are all database behaviour, so a mock would prove nothing.
/// </summary>
[Collection("postgres")]
public class KnowledgeIngestTests(PostgresFixture fx)
{
    private KnowledgeIngester NewIngester(FakeEmbeddingGenerator embeddings, string knowledgeRoot) =>
        new(fx.DbFactory,
            embeddings,
            new ConfigurationBuilder().Build(),
            new TestHostEnvironment(knowledgeRoot),
            NullLogger<KnowledgeIngester>.Instance);

    [Fact]
    public async Task Ingest_loads_embeds_skips_and_re_embeds_only_what_changed()
    {
        var source = FindKnowledgeFolder();

        // ---------- first run: everything is new ----------
        var first = new FakeEmbeddingGenerator();
        var results = await NewIngester(first, source).IngestAsync(source);

        Assert.Equal(5, results.Count);
        Assert.All(results, r => Assert.False(r.Skipped));
        Assert.All(results, r => Assert.True(r.ChunkCount > 0, $"{r.Slug} produced no chunks"));
        Assert.Equal(first.Requested.Count, results.Sum(r => r.ChunkCount));

        await using (var db = await fx.DbFactory.CreateDbContextAsync())
        {
            var documents = await db.KnowledgeDocuments.OrderBy(d => d.Slug).ToListAsync();
            Assert.Equal(5, documents.Count);

            // The audience column matches the folder the file came from — visibility is data.
            foreach (var document in documents)
            {
                Assert.StartsWith(document.Audience + "/", document.SourcePath, StringComparison.Ordinal);
                Assert.True(KnowledgeAudience.IsValid(document.Audience), document.Audience);
                Assert.NotEmpty(document.Title);
                Assert.Equal(64, document.ContentHash.Length);
            }

            Assert.Equal(3, documents.Count(d => d.Audience == KnowledgeAudience.Public));
            Assert.Equal(2, documents.Count(d => d.Audience == KnowledgeAudience.Staff));

            var chunks = await db.KnowledgeChunks.ToListAsync();
            Assert.NotEmpty(chunks);
            Assert.All(chunks, c => Assert.Equal(768, c.Embedding.Memory.Length));
            Assert.All(chunks, c => Assert.True(c.TokenEstimate > 0));

            // Ordinals are contiguous from 0 inside each document.
            foreach (var group in chunks.GroupBy(c => c.DocumentId))
            {
                Assert.Equal(
                    Enumerable.Range(0, group.Count()).ToArray(),
                    group.Select(c => c.Ordinal).OrderBy(o => o).ToArray());
            }
        }

        // ---------- second run: nothing changed, so nothing is embedded ----------
        var second = new FakeEmbeddingGenerator();
        var again = await NewIngester(second, source).IngestAsync(source);

        Assert.Equal(5, again.Count);
        Assert.All(again, r => Assert.True(r.Skipped, $"{r.Slug} was re-embedded for no reason"));
        Assert.Empty(second.Requested);
        Assert.Equal(
            results.Sum(r => r.ChunkCount),
            again.Sum(r => r.ChunkCount));

        // ---------- third run: one edited file in a temporary copy ----------
        var copy = CopyToTemp(source);
        try
        {
            var edited = Path.Combine(copy, "public", "fees-and-payment.md");
            await File.AppendAllTextAsync(edited,
                "\n\n## Late fees\n\nArriving more than 15 minutes late releases the slot.\n");

            var third = new FakeEmbeddingGenerator();
            var afterEdit = await NewIngester(third, copy).IngestAsync(copy);

            var changed = Assert.Single(afterEdit, r => !r.Skipped);
            Assert.Equal("fees-and-payment", changed.Slug);
            Assert.Equal(4, afterEdit.Count(r => r.Skipped));

            // Only the edited document's chunks went to the model.
            Assert.Equal(changed.ChunkCount, third.Requested.Count);
            Assert.Contains(third.Requested, t => t.Contains("Late fees", StringComparison.Ordinal));

            await using var db = await fx.DbFactory.CreateDbContextAsync();
            var document = await db.KnowledgeDocuments.SingleAsync(d => d.Slug == "fees-and-payment");
            var stored = await db.KnowledgeChunks
                .Where(c => c.DocumentId == document.Id)
                .OrderBy(c => c.Ordinal)
                .ToListAsync();

            // Rebuilt, not appended: no duplicate ordinal survived the delete.
            Assert.Equal(changed.ChunkCount, stored.Count);
            Assert.Equal(stored.Count, stored.Select(c => c.Ordinal).Distinct().Count());
            Assert.Contains(stored, c => c.Heading == "Late fees");
            Assert.All(stored, c => Assert.Equal(768, c.Embedding.Memory.Length));
        }
        finally
        {
            Directory.Delete(copy, recursive: true);
        }
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

    private static string CopyToTemp(string source)
    {
        var target = Path.Combine(Path.GetTempPath(), "cliniclive-knowledge-" + Guid.NewGuid().ToString("N"));
        foreach (var folder in Directory.GetDirectories(source))
        {
            var destination = Path.Combine(target, Path.GetFileName(folder));
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(folder, "*.md"))
            {
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
            }
        }

        return target;
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
