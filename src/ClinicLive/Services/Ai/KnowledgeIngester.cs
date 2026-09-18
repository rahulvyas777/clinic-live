using System.Security.Cryptography;
using System.Text;
using ClinicLive.Data;
using ClinicLive.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Pgvector;

namespace ClinicLive.Services.Ai;

/// <summary>What one document did on this run. The <c>ingest</c> command prints these as a table.</summary>
public record IngestResult(string Slug, string Audience, int ChunkCount, bool Skipped);

/// <summary>
/// Reads the clinic's markdown under <c>knowledge/public</c> and <c>knowledge/staff</c>, chunks it,
/// embeds each chunk with nomic-embed-text and stores the result in PostgreSQL.
/// <para>
/// Ingest is idempotent: a document whose SHA-256 has not changed is skipped without a single call
/// to the model. A changed document has its chunks deleted and rebuilt — cheaper to reason about
/// than diffing chunks, and the whole corpus is five files.
/// </para>
/// </summary>
public sealed class KnowledgeIngester(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    IEmbeddingGenerator<string, Embedding<float>> embeddings,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<KnowledgeIngester> logger)
{
    /// <summary>Ollama takes a batch happily; 16 keeps the request small and the progress visible.</summary>
    private const int BatchSize = 16;

    private static readonly string[] Audiences = [KnowledgeAudience.Public, KnowledgeAudience.Staff];

    public async Task<IReadOnlyList<IngestResult>> IngestAsync(
        string? knowledgePath = null, CancellationToken ct = default)
    {
        var root = ResolveKnowledgeRoot(knowledgePath);
        logger.LogInformation("Ingesting knowledge from {Root}", root.FullName);

        var results = new List<IngestResult>();
        foreach (var audience in Audiences)
        {
            var folder = new DirectoryInfo(Path.Combine(root.FullName, audience));
            if (!folder.Exists)
            {
                logger.LogWarning("No {Audience} folder under {Root}", audience, root.FullName);
                continue;
            }

            foreach (var file in folder.GetFiles("*.md").OrderBy(f => f.Name, StringComparer.Ordinal))
            {
                results.Add(await IngestFileAsync(file, audience, root, ct));
            }
        }

        return results;
    }

    private async Task<IngestResult> IngestFileAsync(
        FileInfo file, string folderAudience, DirectoryInfo root, CancellationToken ct)
    {
        var slug = Path.GetFileNameWithoutExtension(file.Name);
        var markdown = await File.ReadAllTextAsync(file.FullName, ct);
        var hash = Sha256(markdown);

        var parsed = Chunker.Chunk(markdown, slug);
        // The front matter is authoritative; the folder is the fallback, and a typo in the
        // front matter must not widen visibility, so an unknown value falls back too.
        var audience = KnowledgeAudience.IsValid(parsed.Audience) ? parsed.Audience! : folderAudience;
        var sourcePath = Path.GetRelativePath(root.FullName, file.FullName).Replace('\\', '/');

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var document = await db.KnowledgeDocuments.FirstOrDefaultAsync(d => d.Slug == slug, ct);

        if (document is not null && document.ContentHash == hash)
        {
            var existing = await db.KnowledgeChunks.CountAsync(c => c.DocumentId == document.Id, ct);
            logger.LogInformation("{Slug} ({Audience}): unchanged, {Chunks} chunks kept", slug, document.Audience, existing);
            return new IngestResult(slug, document.Audience, existing, Skipped: true);
        }

        if (document is null)
        {
            document = new KnowledgeDocument { Slug = slug };
            db.KnowledgeDocuments.Add(document);
        }
        else
        {
            // Rebuild, don't diff: the cascade on document_id would also do it, but an
            // explicit delete keeps the row (and its id) stable for anything referencing it.
            await db.KnowledgeChunks.Where(c => c.DocumentId == document.Id).ExecuteDeleteAsync(ct);
        }

        document.Title = parsed.Title;
        document.Audience = audience;
        document.SourcePath = sourcePath;
        document.ContentHash = hash;
        document.UpdatedAt = DateTimeOffset.UtcNow;

        var vectors = await EmbedAsync(parsed.Chunks.Select(c => c.Text).ToList(), ct);

        for (var i = 0; i < parsed.Chunks.Count; i++)
        {
            var chunk = parsed.Chunks[i];
            db.KnowledgeChunks.Add(new KnowledgeChunk
            {
                Document = document,
                Ordinal = chunk.Ordinal,
                Heading = chunk.Heading,
                Text = chunk.Text,
                TokenEstimate = chunk.TokenEstimate,
                Embedding = new Vector(vectors[i]),
            });
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation("{Slug} ({Audience}): {Chunks} chunks embedded", slug, audience, parsed.Chunks.Count);
        return new IngestResult(slug, audience, parsed.Chunks.Count, Skipped: false);
    }

    private async Task<List<float[]>> EmbedAsync(List<string> texts, CancellationToken ct)
    {
        var vectors = new List<float[]>(texts.Count);
        for (var offset = 0; offset < texts.Count; offset += BatchSize)
        {
            var batch = texts.Skip(offset).Take(BatchSize).ToList();
            var generated = await embeddings.GenerateAsync(batch, cancellationToken: ct);
            vectors.AddRange(generated.Select(e => e.Vector.ToArray()));
        }

        return vectors;
    }

    private static string Sha256(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    /// <summary>
    /// Finds the knowledge folder. Configured path first (absolute or relative to the content
    /// root), then a walk up the parents looking for a folder called <c>knowledge</c> — because
    /// <c>dotnet run --project src/ClinicLive</c> sets the content root to the project folder,
    /// two levels below the repository root where the documents actually live.
    /// </summary>
    private DirectoryInfo ResolveKnowledgeRoot(string? overridePath)
    {
        var configured = overridePath
            ?? configuration["Ai:KnowledgePath"]
            ?? "knowledge";

        if (Path.IsPathRooted(configured) && Directory.Exists(configured))
        {
            return new DirectoryInfo(configured);
        }

        var direct = Path.GetFullPath(Path.Combine(environment.ContentRootPath, configured));
        if (Directory.Exists(direct))
        {
            return new DirectoryInfo(direct);
        }

        var leaf = Path.GetFileName(configured.TrimEnd('/', '\\'));
        for (var dir = new DirectoryInfo(environment.ContentRootPath); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, leaf);
            if (Directory.Exists(candidate))
            {
                return new DirectoryInfo(candidate);
            }
        }

        throw new DirectoryNotFoundException(
            $"No knowledge folder found. Looked for '{configured}' from {environment.ContentRootPath} upward.");
    }
}
