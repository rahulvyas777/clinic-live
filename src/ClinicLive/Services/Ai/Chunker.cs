using System.Text;

namespace ClinicLive.Services.Ai;

/// <summary>What the front matter and the body of a knowledge file turned into.</summary>
/// <param name="Title">From the front matter, or the first <c>#</c> heading, or the slug.</param>
/// <param name="Audience">From the front matter; the ingester falls back to the folder name.</param>
/// <param name="Chunks">In document order, ordinals 0..n-1.</param>
public record ChunkedDocument(string Title, string? Audience, IReadOnlyList<TextChunk> Chunks);

/// <summary>One embeddable slice. <paramref name="Text"/> already carries its context line.</summary>
public record TextChunk(int Ordinal, string? Heading, string Text, int TokenEstimate);

/// <summary>
/// Splits a markdown knowledge document into chunks: first by heading (<c>##</c> and <c>###</c>),
/// then by paragraph so no chunk runs much past <see cref="MaxWords"/> words.
/// <para>
/// Every chunk's text starts with a context line — "Fees and payment &gt; Consultation fees" —
/// because an embedding of "600" under a stripped heading means nothing. The heading is what
/// makes the paragraph findable; without it the vector drifts toward whatever the numbers look
/// like. Front matter is parsed and dropped, never embedded.
/// </para>
/// Pure and static: no database, no HTTP, so the unit tests read like arithmetic.
/// </summary>
public static class Chunker
{
    /// <summary>Roughly 350 words ≈ 450 tokens — a comfortable share of the context window.</summary>
    public const int MaxWords = 350;

    private static readonly char[] Space = [' ', '\t', '\r', '\n'];

    public static ChunkedDocument Chunk(string markdown, string fallbackTitle)
    {
        var (frontTitle, audience, body) = ReadFrontMatter(markdown ?? string.Empty);

        var sections = Split(body);

        // The title: front matter wins, then the document's own "# " heading, then the slug.
        var title = !string.IsNullOrWhiteSpace(frontTitle)
            ? frontTitle!
            : sections.FirstOrDefault(s => s.Level == 1)?.Heading ?? fallbackTitle;

        var chunks = new List<TextChunk>();
        foreach (var section in sections)
        {
            // A "# Title" section's own heading repeats the title; don't print it twice.
            var heading = section.Level > 1 ? section.Heading : null;

            foreach (var block in Pack(section.Paragraphs))
            {
                var text = Compose(title, heading, block);
                chunks.Add(new TextChunk(chunks.Count, heading, text, Estimate(block)));
            }
        }

        return new ChunkedDocument(title, audience, chunks);
    }

    /// <summary>Words × 1.3, rounded. A budget, not a tokenizer.</summary>
    public static int Estimate(string text) =>
        (int)Math.Round(WordCount(text) * 1.3, MidpointRounding.AwayFromZero);

    public static int WordCount(string text) =>
        text.Split(Space, StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>The line every chunk is prefixed with, and Part 7 cites back.</summary>
    public static string ContextLine(string title, string? heading) =>
        string.IsNullOrWhiteSpace(heading) ? title : $"{title} > {heading}";

    private static string Compose(string title, string? heading, string body) =>
        $"{ContextLine(title, heading)}\n{body}";

    /// <summary>
    /// Parses a <c>---</c> fenced header. Only <c>title</c> and <c>audience</c> matter, and the
    /// body returned excludes the header entirely — it is metadata, not knowledge.
    /// </summary>
    private static (string? Title, string? Audience, string Body) ReadFrontMatter(string markdown)
    {
        var text = markdown.Replace("\r\n", "\n").TrimStart('﻿');
        if (!text.StartsWith("---\n", StringComparison.Ordinal))
        {
            return (null, null, text);
        }

        var end = text.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (end < 0)
        {
            return (null, null, text);
        }

        var header = text[4..(end + 1)];
        var body = text[(end + 4)..].TrimStart('\n');

        string? title = null, audience = null;
        foreach (var line in header.Split('\n'))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;

            var key = line[..colon].Trim().ToLowerInvariant();
            var value = line[(colon + 1)..].Trim().Trim('"', '\'');
            if (key == "title") title = value;
            else if (key == "audience") audience = value.ToLowerInvariant();
        }

        return (title, audience, body);
    }

    private sealed record Section(int Level, string? Heading, List<string> Paragraphs);

    /// <summary>One section per heading; text before the first heading belongs to a headless one.</summary>
    private static List<Section> Split(string body)
    {
        var sections = new List<Section>();
        var current = new Section(0, null, []);
        var buffer = new List<string>();

        void FlushParagraph()
        {
            if (buffer.Count == 0) return;
            current.Paragraphs.Add(string.Join("\n", buffer).Trim());
            buffer.Clear();
        }

        void FlushSection()
        {
            FlushParagraph();
            if (current.Paragraphs.Count > 0 || current.Heading is not null)
            {
                sections.Add(current);
            }
        }

        foreach (var line in body.Replace("\r\n", "\n").Split('\n'))
        {
            var level = HeadingLevel(line);
            if (level > 0)
            {
                FlushSection();
                current = new Section(level, line[level..].Trim(), []);
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                FlushParagraph();
                continue;
            }

            buffer.Add(line.TrimEnd());
        }

        FlushSection();

        // A "# Title" section with nothing but the heading carries no knowledge; drop it.
        return sections.Where(s => s.Paragraphs.Count > 0).ToList();
    }

    private static int HeadingLevel(string line)
    {
        var hashes = 0;
        while (hashes < line.Length && line[hashes] == '#') hashes++;
        return hashes is > 0 and <= 6 && hashes < line.Length && line[hashes] == ' ' ? hashes : 0;
    }

    /// <summary>
    /// Greedily fills a chunk with whole paragraphs up to <see cref="MaxWords"/>. A single
    /// paragraph longer than the cap (a long table) is split on its own lines rather than
    /// mid-sentence, so a table row never lands in two chunks.
    /// </summary>
    private static List<string> Pack(List<string> paragraphs)
    {
        var blocks = new List<string>();
        var sb = new StringBuilder();
        var words = 0;

        void Flush()
        {
            if (sb.Length == 0) return;
            blocks.Add(sb.ToString().Trim());
            sb.Clear();
            words = 0;
        }

        foreach (var paragraph in paragraphs)
        {
            foreach (var piece in SplitOversized(paragraph))
            {
                var pieceWords = WordCount(piece);
                if (words > 0 && words + pieceWords > MaxWords)
                {
                    Flush();
                }

                if (sb.Length > 0) sb.Append("\n\n");
                sb.Append(piece);
                words += pieceWords;
            }
        }

        Flush();
        return blocks;
    }

    private static IEnumerable<string> SplitOversized(string paragraph)
    {
        if (WordCount(paragraph) <= MaxWords)
        {
            yield return paragraph;
            yield break;
        }

        var sb = new StringBuilder();
        var words = 0;
        foreach (var line in paragraph.Split('\n'))
        {
            var lineWords = WordCount(line);
            if (words > 0 && words + lineWords > MaxWords)
            {
                yield return sb.ToString().Trim();
                sb.Clear();
                words = 0;
            }

            if (sb.Length > 0) sb.Append('\n');
            sb.Append(line);
            words += lineWords;
        }

        if (sb.Length > 0) yield return sb.ToString().Trim();
    }
}
