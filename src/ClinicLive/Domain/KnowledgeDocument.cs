namespace ClinicLive.Domain;

/// <summary>
/// One markdown file under <c>knowledge/</c>, as the database knows it. The file itself stays
/// the source of truth; this row is the bookkeeping that lets ingest skip a document whose text
/// has not changed.
/// </summary>
public class KnowledgeDocument
{
    public int Id { get; set; }

    /// <summary>File name without the extension — <c>fees-and-payment</c>. Stable identity.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>From the front matter; shown in a citation and prefixed onto every chunk.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// <c>public</c> or <c>staff</c>. Visibility is a column, never a prompt instruction
    /// (docs/private.md), and the database refuses anything else via a check constraint.
    /// </summary>
    public string Audience { get; set; } = KnowledgeAudience.Public;

    /// <summary>Path relative to the knowledge folder — <c>public/fees-and-payment.md</c>.</summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>SHA-256 of the file's text. Same hash on the next run → nothing is re-embedded.</summary>
    public string ContentHash { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<KnowledgeChunk> Chunks { get; set; } = [];
}

/// <summary>The only two audiences the schema allows. Plain strings, not a PG enum (docs/schema.md).</summary>
public static class KnowledgeAudience
{
    public const string Public = "public";
    public const string Staff = "staff";

    public static bool IsValid(string? value) => value is Public or Staff;
}
