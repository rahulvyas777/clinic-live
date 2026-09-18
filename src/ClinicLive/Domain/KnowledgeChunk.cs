using Pgvector;

namespace ClinicLive.Domain;

/// <summary>
/// A slice of a knowledge document, small enough to fit in a prompt and large enough to answer
/// a question on its own. <see cref="Text"/> already carries the document title and heading on
/// its first line, because that is what gets embedded.
/// </summary>
public class KnowledgeChunk
{
    public int Id { get; set; }

    public int DocumentId { get; set; }
    public KnowledgeDocument Document { get; set; } = null!;

    /// <summary>Position within the document, from 0. Unique with <see cref="DocumentId"/>.</summary>
    public int Ordinal { get; set; }

    /// <summary>The heading this chunk sits under, if any — <c>Consultation fees</c>.</summary>
    public string? Heading { get; set; }

    public string Text { get; set; } = string.Empty;

    /// <summary>Words × 1.3, rounded. Good enough to budget a prompt; not a real tokenizer.</summary>
    public int TokenEstimate { get; set; }

    /// <summary>nomic-embed-text output: 768 dimensions, cosine distance (docs/private.md).</summary>
    public Vector Embedding { get; set; } = null!;
}
