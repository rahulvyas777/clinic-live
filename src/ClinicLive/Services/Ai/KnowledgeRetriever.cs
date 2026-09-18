using ClinicLive.Data;
using ClinicLive.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace ClinicLive.Services.Ai;

/// <summary>
/// One chunk the search found, with everything a prompt line and a citation need.
/// </summary>
/// <param name="ChunkId">The <c>knowledge_chunk</c> row, so a citation can be traced back.</param>
/// <param name="DocumentTitle">"Fees and payment" — the human half of the citation.</param>
/// <param name="DocumentSlug">"fees-and-payment" — the file half.</param>
/// <param name="Heading">The section this chunk sits under, if the document had one.</param>
/// <param name="Text">The embedded text, context line included (see <see cref="Chunker"/>).</param>
/// <param name="Distance">Cosine distance to the question: 0 is identical, 1 is unrelated.</param>
public record RetrievedChunk(
    int ChunkId,
    string DocumentTitle,
    string DocumentSlug,
    string? Heading,
    string Text,
    double Distance)
{
    /// <summary>"Fees and payment &gt; Consultation fees" — the same line the chunker embedded.</summary>
    public string Label => Chunker.ContextLine(DocumentTitle, Heading);

    /// <summary>What the page hangs in a tooltip: enough to see why this chunk was picked.</summary>
    public string Preview => Text.Length <= 200 ? Text : Text[..200];

    /// <summary>
    /// The chunk without its leading context line. The prompt prints that line itself as
    /// "[n] Title &gt; Heading", so repeating it inside the body only spends tokens twice.
    /// </summary>
    public string Body
    {
        get
        {
            var newline = Text.IndexOf('\n');
            return newline >= 0 && Text[..newline].TrimEnd('\r') == Label
                ? Text[(newline + 1)..].Trim()
                : Text.Trim();
        }
    }
}

/// <summary>
/// Nearest-neighbour search over the clinic's own documents: embed the question with the same
/// model that embedded the corpus, then let pgvector sort the chunks by cosine distance.
/// <para>
/// The audience filter is a <c>WHERE</c> on the document row, never a filter applied to the rows
/// after they arrive. docs/private.md: "The patient assistant never sees a staff chunk. Enforced
/// in the SQL." A chunk that the query cannot return is a chunk no prompt can leak, no matter
/// what the model is told or what a user types.
/// </para>
/// </summary>
public sealed class KnowledgeRetriever(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    IEmbeddingGenerator<string, Embedding<float>> embeddings,
    ILogger<KnowledgeRetriever> logger)
{
    /// <param name="question">The user's words, embedded as-is.</param>
    /// <param name="audience">"staff" sees both audiences; anything else sees public only.</param>
    /// <param name="take">How many chunks to bring back — <c>Ai:MaxContextChunks</c>.</param>
    /// <param name="ct">Cancelled when the circuit goes away.</param>
    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        string question,
        string audience,
        int take,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question) || take <= 0)
        {
            return [];
        }

        var generated = await embeddings.GenerateAsync([question], cancellationToken: ct);
        var vector = new Vector(generated[0].Vector.ToArray());

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // Unknown audience values fail closed: only the exact string "staff" widens the view.
        var visible = audience == KnowledgeAudience.Staff
            ? db.KnowledgeChunks.Where(c =>
                c.Document.Audience == KnowledgeAudience.Staff ||
                c.Document.Audience == KnowledgeAudience.Public)
            : db.KnowledgeChunks.Where(c =>
                c.Document.Audience == KnowledgeAudience.Public);

        var found = await visible
            .AsNoTracking()
            .OrderBy(c => c.Embedding.CosineDistance(vector))
            .Take(take)
            .Select(c => new RetrievedChunk(
                c.Id,
                c.Document.Title,
                c.Document.Slug,
                c.Heading,
                c.Text,
                c.Embedding.CosineDistance(vector)))
            .ToListAsync(ct);

        // The question is not logged — it may carry a patient's words. The distances are.
        logger.LogInformation(
            "Knowledge search for {Audience}: {Count} chunks, nearest {Nearest:F3}",
            audience, found.Count, found.Count == 0 ? double.NaN : found[0].Distance);

        return found;
    }
}
