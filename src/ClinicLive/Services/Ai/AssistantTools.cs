using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace ClinicLive.Services.Ai;

/// <summary>
/// One patient in the queue, as the model is allowed to see them. First name and last initial —
/// exactly what the waiting-room TV already shows — the confirmation code, and how long they have
/// waited. No full name, no phone number, no date of birth, no reason for the visit. The model
/// cannot leak what it was never handed, which is the same argument the retrieval SQL makes in
/// Part 7: the limit is in the query, not in the prompt.
/// </summary>
/// <param name="Name">"Maria G." — the masked display name <see cref="QueueService"/> builds.</param>
/// <param name="Code">The confirmation code, so staff can act on the answer.</param>
/// <param name="MinutesWaited">Minutes since check-in (or until they were called).</param>
/// <param name="Status">"waiting", "called" or "done" — the status that was asked for.</param>
public sealed record QueuePatient(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("minutes_waited")] int MinutesWaited,
    [property: JsonPropertyName("status")] string Status);

/// <summary>
/// The assistant's one tool. Part 7 gave the model the clinic's documents; a document cannot
/// answer "who has waited the longest right now?", because that changes every minute. This does:
/// the model asks for the queue, the function runs against the same <see cref="QueueService"/>
/// the staff screen uses, and the JSON comes back as another turn in the conversation.
/// <para>
/// Scoped, because <see cref="QueueService"/> is scoped — it opens a short-lived DbContext per
/// call. The <see cref="AIFunction"/> is built per circuit from that instance, so nothing static
/// ever holds a database handle.
/// </para>
/// </summary>
public sealed class AssistantTools(QueueService queue, ILogger<AssistantTools> logger)
{
    /// <summary>The name the model sees, and the name the guard logs.</summary>
    public const string GetQueueName = "get_queue";

    /// <summary>
    /// What the model reads when it decides whether to call. The second sentence is the fence:
    /// Part 2's bake-off caught Llama 3.1 calling a queue tool to answer a question about
    /// opening hours. A description can only discourage that — the real fence is the classifier
    /// in <see cref="AssistantService"/>, which does not offer the tool at all.
    /// </summary>
    public const string GetQueueDescription =
        "Returns the patients in the clinic queue right now with how long each has waited, " +
        "longest first. Only for questions about the current queue or waiting times.";

    /// <summary>The tool as Microsoft.Extensions.AI hands it to the model, bound to this scope.</summary>
    public AIFunction GetQueueFunction() =>
        AIFunctionFactory.Create(GetQueueAsync, GetQueueName, GetQueueDescription);

    /// <summary>
    /// The function body the model's call runs. Never throws at the model: an unknown status is
    /// read as "waiting" rather than returned as an error the model would then try to explain.
    /// </summary>
    public async Task<IReadOnlyList<QueuePatient>> GetQueueAsync(
        [Description("Which part of the queue to report: \"waiting\", \"called\" or \"done\".")]
        string status = "waiting",
        CancellationToken ct = default)
    {
        var wanted = status?.Trim().ToLowerInvariant() switch
        {
            "called" => "called",
            "done" => "done",
            _ => "waiting",
        };

        var lines = await queue.GetLinesAsync(wanted, ct);

        // The patients are not logged — only that the model asked and how much it got back.
        logger.LogInformation("{Tool} ran for status {Status}: {Count} patients", GetQueueName, wanted, lines.Count);

        return lines
            .Select(l => new QueuePatient(l.Name, l.Code, l.MinutesWaited, l.Status))
            .ToList();
    }
}
