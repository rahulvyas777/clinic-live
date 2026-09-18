using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace ClinicLive.Services.Ai;

/// <summary>
/// The clinic's assistant. One question in, a stream of text out, from a model running
/// on this machine — no token about a patient leaves the building.
///
/// Part 5 is the plumbing: system prompt + question + streaming. Part 7 adds retrieval,
/// which is why <c>audience</c> is already a parameter: the staff assistant will see all
/// documents, the patient assistant only the public ones. Visibility will be enforced in
/// the SQL, never in this prompt.
/// </summary>
public class AssistantService(IChatClient chat, IConfiguration config, ILogger<AssistantService> logger)
{
    /// <summary>
    /// The whole safety policy, fixed in code. {0} is the clinic's name.
    /// Refusal wording is not generated — the model is told the exact sentences to use.
    /// </summary>
    public const string SystemPromptTemplate = """
        You are the assistant for {0}. You help with questions about this clinic:
        opening hours, appointments, what to bring, where to park, how to check in.

        Answer briefly — two or three short sentences, plain language, no lists unless asked.

        You are not a clinician and you never act like one.
        Never give medical advice, never name a medicine, never give a dose, never diagnose.
        For anything medical — symptoms, treatment, test results, whether something is serious —
        answer only: please speak to the doctor.

        If the question describes an emergency, tell the person to call 108 now.

        If you do not know the answer, or the clinic has not told you, say exactly:
        I don't know that; please ask reception.

        Never invent times, prices, phone numbers or staff names.
        """;

    private readonly string _clinicName = config["Clinic:Name"] ?? "the clinic";

    /// <summary>The prompt as the model will see it. Public so a unit test can read it.</summary>
    public static string BuildSystemPrompt(string clinicName) =>
        string.Format(SystemPromptTemplate, clinicName);

    /// <summary>The prompt for this clinic, built from <c>Clinic:Name</c>.</summary>
    public string SystemPrompt => BuildSystemPrompt(_clinicName);

    /// <summary>
    /// Ask the assistant. Yields text deltas as the model produces them, so the page can
    /// paint a word at a time instead of staring at a spinner for twenty seconds.
    /// </summary>
    /// <param name="question">What the user typed.</param>
    /// <param name="audience">"staff" or "patient" — decides document visibility from Part 7.</param>
    /// <param name="ct">Cancelled when the circuit goes away.</param>
    public async IAsyncEnumerable<string> AskAsync(
        string question,
        string audience,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // The question itself is never logged: it may carry a patient's words.
        logger.LogInformation("Assistant question from {Audience} ({Length} chars)", audience, question.Length);

        List<AiChatMessage> messages =
        [
            new(ChatRole.System, SystemPrompt),
            new(ChatRole.User, question),
        ];

        await foreach (var update in chat.GetStreamingResponseAsync(messages, cancellationToken: ct))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return update.Text;
            }
        }
    }
}
