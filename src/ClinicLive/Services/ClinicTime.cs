namespace ClinicLive.Services;

/// <summary>
/// The one place that knows what timezone the clinic lives in.
///
/// The bug this class fixes (Part 10): "today" and "09:00" were computed straight
/// from UTC. A clinic east of Greenwich saw yesterday's slots in the local evening,
/// and every slot was labelled with UTC times. Rule: store UTC, but decide
/// "which day is it?" and "what does 9am mean?" in the CLINIC's zone.
/// </summary>
public class ClinicTime(IConfiguration config)
{
    public TimeZoneInfo Zone { get; } =
        TimeZoneInfo.FindSystemTimeZoneById(config["Clinic:TimeZone"] ?? "UTC");

    /// <summary>Today, as the clinic's wall calendar sees it.</summary>
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone));

    /// <summary>A clinic-local date + wall time as the UTC instant it happens at.</summary>
    public DateTime ToUtc(DateOnly date, TimeOnly time) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(time, DateTimeKind.Unspecified), Zone);

    /// <summary>UTC bounds of one clinic-local day: [start, end).</summary>
    public (DateTime StartUtc, DateTime EndUtc) DayBoundsUtc(DateOnly date) =>
        (ToUtc(date, TimeOnly.MinValue), ToUtc(date.AddDays(1), TimeOnly.MinValue));

    /// <summary>Render a stored UTC instant as clinic wall time.</summary>
    public string Local(DateTime utc, string format = "HH:mm") =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone).ToString(format);
}
