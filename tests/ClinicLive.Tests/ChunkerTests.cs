using ClinicLive.Services.Ai;

namespace ClinicLive.Tests;

/// <summary>
/// The chunker decides what the model will ever be able to find. These tests are pure
/// string arithmetic — no database, no Ollama.
/// </summary>
public class ChunkerTests
{
    private const string Sample = """
        ---
        title: Fees and payment
        audience: public
        ---

        # Fees and payment

        ## Consultation fees

        General consultation costs 600. A follow-up within 7 days costs 300.

        Senior citizens pay 450.

        ## Payment

        Cash, UPI and cards are accepted at reception.

        ### Insurance

        The clinic does not bill insurance directly.
        """;

    [Fact]
    public void Front_matter_is_parsed_and_never_embedded()
    {
        var doc = Chunker.Chunk(Sample, "fees-and-payment");

        Assert.Equal("Fees and payment", doc.Title);
        Assert.Equal("public", doc.Audience);

        // The "---" fence and the raw "title:" / "audience:" keys must not reach a vector.
        Assert.All(doc.Chunks, c => Assert.DoesNotContain("---", c.Text, StringComparison.Ordinal));
        Assert.All(doc.Chunks, c => Assert.DoesNotContain("audience:", c.Text, StringComparison.Ordinal));
    }

    [Fact]
    public void Every_chunk_starts_with_its_title_and_heading()
    {
        var doc = Chunker.Chunk(Sample, "fees-and-payment");

        Assert.NotEmpty(doc.Chunks);
        foreach (var chunk in doc.Chunks)
        {
            var firstLine = chunk.Text.Split('\n')[0];
            Assert.Equal(Chunker.ContextLine("Fees and payment", chunk.Heading), firstLine);
            Assert.StartsWith("Fees and payment", firstLine, StringComparison.Ordinal);
        }

        // "600" is meaningless on its own; the heading is what makes it findable.
        var fees = Assert.Single(doc.Chunks, c => c.Text.Contains("600", StringComparison.Ordinal));
        Assert.Equal("Consultation fees", fees.Heading);
        Assert.StartsWith("Fees and payment > Consultation fees", fees.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Third_level_headings_split_too()
    {
        var doc = Chunker.Chunk(Sample, "fees-and-payment");

        Assert.Contains(doc.Chunks, c => c.Heading == "Insurance");
        Assert.Contains(doc.Chunks, c => c.Heading == "Payment");
    }

    [Fact]
    public void No_chunk_runs_far_past_the_word_cap()
    {
        // One heading, forty paragraphs of thirty words: far past the cap, so it must split.
        var paragraph = string.Join(" ", Enumerable.Repeat("word", 30));
        var markdown = "## Long section\n\n" + string.Join("\n\n", Enumerable.Repeat(paragraph, 40));

        var doc = Chunker.Chunk(markdown, "long");

        Assert.True(doc.Chunks.Count > 1, "a 1,200-word section must not stay in one chunk");
        foreach (var chunk in doc.Chunks)
        {
            // The context line adds a few words on top of the body's budget.
            Assert.True(
                Chunker.WordCount(chunk.Text) <= Chunker.MaxWords + 10,
                $"chunk {chunk.Ordinal} has {Chunker.WordCount(chunk.Text)} words");
        }

        Assert.All(doc.Chunks, c => Assert.Equal("Long section", c.Heading));
    }

    [Fact]
    public void Ordinals_run_from_zero_without_a_gap()
    {
        var doc = Chunker.Chunk(Sample, "fees-and-payment");

        Assert.Equal(
            Enumerable.Range(0, doc.Chunks.Count).ToArray(),
            doc.Chunks.Select(c => c.Ordinal).ToArray());
    }

    [Fact]
    public void Token_estimate_is_words_times_one_point_three()
    {
        // Ten words → 13. The estimate covers the body, which is what a prompt pays for.
        var markdown = "## Counting\n\n" + string.Join(" ", Enumerable.Repeat("word", 10));

        var chunk = Assert.Single(Chunker.Chunk(markdown, "counting").Chunks);

        Assert.Equal(13, chunk.TokenEstimate);
        Assert.Equal(13, Chunker.Estimate(string.Join(" ", Enumerable.Repeat("word", 10))));
    }

    [Fact]
    public void A_document_without_front_matter_falls_back_to_the_slug()
    {
        var doc = Chunker.Chunk("Just a line of text.", "no-front-matter");

        Assert.Equal("no-front-matter", doc.Title);
        Assert.Null(doc.Audience);
        Assert.Single(doc.Chunks);
    }
}
