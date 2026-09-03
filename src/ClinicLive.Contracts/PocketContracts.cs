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
