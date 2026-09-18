using ClinicLive.Services.Ai;

namespace ClinicLive.Tests;

/// <summary>
/// The safety policy lives in one const string. These tests are the lock on it:
/// nothing here talks to Ollama, so they run on CI with no model and no GPU.
/// </summary>
public class AiPromptTests
{
    [Fact]
    public void System_prompt_names_the_clinic()
    {
        var prompt = AssistantService.BuildSystemPrompt("Sunrise Family Clinic");

        Assert.Contains("Sunrise Family Clinic", prompt);
        Assert.DoesNotContain("{0}", prompt);
    }

    [Fact]
    public void System_prompt_carries_the_three_safety_sentences()
    {
        var prompt = AssistantService.BuildSystemPrompt("Sunrise Family Clinic");

        // Emergency, medical question, and "I don't know" — the three exits the model is given.
        Assert.Contains("call 108", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("speak to the doctor", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("I don't know that; please ask reception.", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void System_prompt_asks_for_a_sources_line()
    {
        var prompt = AssistantService.BuildSystemPrompt("Sunrise Family Clinic");

        Assert.Contains("CLINIC DOCUMENTS", prompt, StringComparison.Ordinal);
        Assert.Contains("Sources: [1], [3]", prompt, StringComparison.Ordinal);
    }

    /// <summary>
    /// The guard reads citations, and qwen2.5 does not always give the Sources line a line of
    /// its own — the first live run ended "...see your history. Sources: [1]" and was thrown
    /// away as uncited. A citation is a citation wherever it sits.
    /// </summary>
    [Fact]
    public void A_citation_counts_even_when_it_shares_the_last_line()
    {
        const string inline = "Bring your appointment code and a photo ID. Sources: [1], [3]";

        Assert.True(AssistantService.HasCitation(inline));
        Assert.Equal([1, 3], AssistantService.CitedNumbers(inline));

        Assert.True(AssistantService.HasCitation("Arrive ten minutes early.\nSources: [2]"));
        Assert.Equal([2], AssistantService.CitedNumbers("Arrive ten minutes early.\nSources: [2]"));

        Assert.False(AssistantService.HasCitation("Bring your appointment code and a photo ID."));
        Assert.Empty(AssistantService.CitedNumbers("Bring your appointment code and a photo ID."));

        Assert.True(AssistantService.IsRefusal(AssistantService.RefusalText));
        Assert.False(AssistantService.IsRefusal(AssistantService.RefusalText + " Sources: [1]"));
    }

    [Fact]
    public void System_prompt_forbids_medicine_names_and_doses()
    {
        var prompt = AssistantService.BuildSystemPrompt("Sunrise Family Clinic");

        Assert.Contains("never name a medicine", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never give a dose", prompt, StringComparison.OrdinalIgnoreCase);
    }
}
