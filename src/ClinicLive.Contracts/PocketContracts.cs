using System.Text.Json.Serialization;

namespace ClinicLive.Contracts;

/// <summary>Where the clinic is and when it's open — everything the app shows before you even have a booking.</summary>
public sealed record ClinicInfo(
    string Name,
    string AddressLine1,
    string AddressLine2,
    string Phone,
    double Latitude,
    double Longitude,
    string TimeZone,
    string OpeningHours);

/// <summary>
/// Lifecycle of one appointment as the patient sees it. Mirrors the server enum by NAME,
/// and travels as a name ("CheckedIn", not 1) so the JSON reads like English in a curl.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisitStatus>))]
public enum VisitStatus
{
    Booked,
    CheckedIn,
    InProgress,
    Done,
    Cancelled,
    NoShow,
}

/// <summary>
/// One appointment, looked up by its confirmation code. First name only — the code
/// is the patient's credential, and a leaked screen should reveal as little as possible.
/// </summary>
public sealed record VisitDto(
    string Code,
    string FirstName,
    DateTime StartsAtUtc,
    string StartsAtLocal,
    string DayLocal,
    VisitStatus Status,
    bool IsToday,
    bool CanCheckIn,
    int? Position,
    int WaitingCount,
    string? NowServing);

public sealed record CheckInResponse(bool Success, string? Error, int Position);

/// <summary>"This phone belongs to this visit": a push token, so the clinic can reach the patient when the app is closed.</summary>
public sealed record DeviceRegistrationRequest(string Platform, string Token);

/// <summary>The public waiting-room board, exactly what the wall TV shows (names masked).</summary>
public sealed record QueueDto(string? NowServing, IReadOnlyList<string> Waiting);

/// <summary>
/// One patient question for the clinic's assistant (season four, Part 9). A question and
/// nothing else: there is deliberately no audience field, because a caller that could name its
/// own audience could ask for the staff documents. The server decides that, always "public".
/// </summary>
public sealed record AssistantQuestion(string Question);

/// <summary>
/// The handful of facts both ends of <c>POST /api/pocket/assistant</c> have to agree on. The
/// endpoint answers with a stream of plain text, not a JSON object, so the contract is small on
/// purpose: what a question may be, where the citations ride, and the one sentence the
/// assistant is allowed to fail with.
/// </summary>
public static class AssistantContract
{
    /// <summary>Shorter than this is a slip of the thumb, not a question.</summary>
    public const int MinQuestionLength = 3;

    /// <summary>Longer than this is a paste, and a kiosk queue is not the place for it.</summary>
    public const int MaxQuestionLength = 300;

    /// <summary>What a 400 says. Short enough to print under the box the patient typed in.</summary>
    public const string LengthError = "A question has to be between 3 and 300 characters.";

    /// <summary>What a 429 says. The limit is per client per minute, so waiting really does fix it.</summary>
    public const string TooManyError = "Too many questions just now. Please try again in a minute.";

    /// <summary>
    /// The one sentence the assistant may fail with, fixed in code and never generated
    /// (docs/private.md). The server's own copy is <c>AssistantService.RefusalText</c>; a test
    /// pins the two together so they can never drift.
    /// </summary>
    public const string Refusal = "I don't know that; please ask reception.";

    /// <summary>
    /// Where the citations ride. The body is the answer as the model writes it, token by token,
    /// so the documents it stands on travel in a header instead — they are known before the
    /// first token exists, which is exactly while a header can still be set.
    /// One label per entry: <c>[1] Patient information &gt; What to bring</c>.
    /// </summary>
    public const string CitationHeader = "X-Clinic-Sources";

    /// <summary>The separator inside that header, since a header value cannot hold newlines.</summary>
    public const string CitationSeparator = " | ";

    /// <summary>
    /// True when this is a question the assistant will take. Trimmed first: the kiosk's big
    /// keyboard leaves trailing spaces behind, and " ? " is not a three-character question.
    /// </summary>
    public static bool IsAskable(string? question) =>
        question is not null
        && question.Trim().Length is >= MinQuestionLength and <= MaxQuestionLength;

    /// <summary>
    /// The finished answer as the patient should see it.
    /// <para>
    /// A stream cannot be unsent. When the server's citation guard fires mid-answer it writes
    /// the refusal as the last thing on the wire (PocketEndpoints), so an answer that ENDS with
    /// that fixed sentence is an answer that was withdrawn — and all that may be shown is the
    /// sentence itself. Fixed text is what makes this safe to test for: nothing else in the
    /// clinic's documents ends that way, and the model is never the one who wrote it.
    /// </para>
    /// </summary>
    public static string NormalizeAnswer(string? streamed)
    {
        var text = (streamed ?? string.Empty).Trim();
        return text.EndsWith(Refusal, StringComparison.Ordinal) ? Refusal : text;
    }

    /// <summary>True when the finished answer is the refusal and nothing else.</summary>
    public static bool IsRefusal(string? answer) =>
        string.Equals(NormalizeAnswer(answer), Refusal, StringComparison.Ordinal);

    /// <summary>
    /// The citation lines to print under an answer: the documents its "Sources: [1], [3]" line
    /// named, out of the ones the <see cref="CitationHeader"/> offered. A refusal cites nothing.
    /// An answer that named no usable number still stands on what it was given, so the nearest
    /// document is shown rather than a bare paragraph with no provenance at all.
    /// </summary>
    public static IReadOnlyList<string> Citations(string? answer, string? header)
    {
        // The refusal test runs on the text WITHOUT its Sources line, because a model that has
        // nothing to say will cheerfully say so and then cite the documents it did not use. The
        // first live kiosk run printed "I don't know that; please ask reception." over two
        // citations — "never both, never neither" (docs/private.md), and that was both.
        if (string.IsNullOrWhiteSpace(header) || IsRefusal(WithoutSourcesLine(answer ?? string.Empty)))
        {
            return [];
        }

        var offered = header.Split(
            CitationSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (offered.Length == 0)
        {
            return [];
        }

        var picked = CitedNumbers(answer ?? string.Empty)
            .Select(n => Array.Find(offered, o => o.StartsWith($"[{n}] ", StringComparison.Ordinal)))
            .Where(o => o is not null)
            .Select(o => o!)
            .ToList();

        if (picked.Count == 0)
        {
            picked.Add(offered[0]);
        }

        // "[2] Fees and payment" reads as "Fees and payment" to someone who never saw a prompt.
        return picked.Select(o => o[(o.IndexOf(']') + 1)..].Trim()).ToList();
    }

    /// <summary>
    /// The numbers on the answer's last "Sources:" line, in the order the model wrote them.
    /// The server has its own copy of this (<c>AssistantService.CitedNumbers</c>) because it
    /// reads the same line for the guard; this one is for the two clients that only ever see
    /// the finished text.
    /// </summary>
    public static IReadOnlyList<int> CitedNumbers(string answer)
    {
        var start = answer.LastIndexOf("Sources:", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return [];
        }

        var end = answer.IndexOf('\n', start);
        var line = end < 0 ? answer[start..] : answer[start..end];

        var numbers = new List<int>();
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] != '[') continue;

            var close = line.IndexOf(']', i + 1);
            if (close < 0) break;

            if (int.TryParse(line[(i + 1)..close], out var n) && !numbers.Contains(n))
            {
                numbers.Add(n);
            }

            i = close;
        }

        return numbers;
    }

    /// <summary>
    /// The answer without its "Sources: [1], [3]" line. The staff assistant leaves that line in
    /// the bubble — a receptionist reads the numbers. A patient does not: the kiosk and the app
    /// print the document titles underneath instead, so the line itself is noise in giant type.
    /// </summary>
    public static string WithoutSourcesLine(string answer)
    {
        if (string.IsNullOrEmpty(answer))
        {
            return string.Empty;
        }

        var start = answer.LastIndexOf("Sources:", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return answer.Trim();
        }

        var end = answer.IndexOf('\n', start);
        var without = end < 0 ? answer[..start] : answer[..start] + answer[(end + 1)..];
        return without.Trim();
    }
}
