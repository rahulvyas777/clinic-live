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
    public void System_prompt_forbids_medicine_names_and_doses()
    {
        var prompt = AssistantService.BuildSystemPrompt("Sunrise Family Clinic");

        Assert.Contains("never name a medicine", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never give a dose", prompt, StringComparison.OrdinalIgnoreCase);
    }
}
