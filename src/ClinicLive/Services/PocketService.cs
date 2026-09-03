using ClinicLive.Contracts;
using ClinicLive.Data;
using ClinicLive.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClinicLive.Services;

/// <summary>
/// Everything the Pocket app may ask the clinic. It is a THIN translation layer:
/// the rules (who can check in, queue order, "today" in the clinic's zone) stay in
/// BookingService / QueueService / ClinicTime, exactly where the web app finds them.
/// A second front door must not grow a second set of rules.
/// </summary>
public class PocketService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    QueueService queue,
    ClinicTime clinic,
    IConfiguration config)
{
    public ClinicInfo GetClinicInfo()
    {
        var c = config.GetSection("Clinic");
        return new ClinicInfo(
            c["Name"] ?? "ClinicLive",
            c["AddressLine1"] ?? "",
            c["AddressLine2"] ?? "",
            c["Phone"] ?? "",
            double.TryParse(c["Latitude"], System.Globalization.CultureInfo.InvariantCulture, out var lat) ? lat : 0,
            double.TryParse(c["Longitude"], System.Globalization.CultureInfo.InvariantCulture, out var lng) ? lng : 0,
            clinic.Zone.Id,
            c["OpeningHours"] ?? "");
    }

    /// <summary>The visit behind a confirmation code, or null. Any date — a patient may look at tomorrow's booking today.</summary>
    public async Task<VisitDto?> GetVisitAsync(string code)
    {
        code = code.Trim().ToUpperInvariant();

        await using var db = await dbFactory.CreateDbContextAsync();
        var appointment = await db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.QueueEntry)
            .FirstOrDefaultAsync(a => a.ConfirmationCode == code);

        if (appointment is null)
        {
            return null;
        }

        var today = clinic.Today;
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(appointment.StartsAt, clinic.Zone));
        var isToday = day == today;

        // The queue is only interesting once you're in it, but "3 people waiting" is
        // useful context before you check in too — it's public on the wall TV anyway.
        var snapshot = await queue.GetSnapshotAsync();
        var index = snapshot.Waiting.FindIndex(w => w.AppointmentId == appointment.Id);

        return new VisitDto(
            Code: code,
            FirstName: FirstNameOf(appointment.Patient.FullName),
            StartsAtUtc: DateTime.SpecifyKind(appointment.StartsAt, DateTimeKind.Utc),
            StartsAtLocal: clinic.Local(appointment.StartsAt),
            DayLocal: DayLabel(day, today),
            Status: Enum.Parse<VisitStatus>(appointment.Status.ToString()),
            IsToday: isToday,
            CanCheckIn: isToday && appointment.Status == AppointmentStatus.Booked,
            Position: index >= 0 ? index + 1 : null,
            WaitingCount: snapshot.Waiting.Count,
            NowServing: snapshot.NowServing?.DisplayName);
    }

    /// <summary>Same rule as the kiosk — literally the same method.</summary>
    public async Task<CheckInResponse> CheckInAsync(string code)
    {
        var result = await queue.CheckInAsync(code);
        return new CheckInResponse(result.Success, result.Error, result.Position);
    }

    /// <summary>What the waiting-room TV shows: masked names only.</summary>
    public async Task<QueueDto> GetQueueAsync()
    {
        var snapshot = await queue.GetSnapshotAsync();
        return new QueueDto(snapshot.NowServing?.DisplayName, snapshot.Waiting.Select(w => w.DisplayName).ToList());
    }

    private static string FirstNameOf(string fullName) =>
        fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? fullName;

    private static string DayLabel(DateOnly day, DateOnly today) =>
        (day.DayNumber - today.DayNumber) switch
        {
            0 => "Today",
            1 => "Tomorrow",
            -1 => "Yesterday",
            _ => day.ToString("ddd d MMM", System.Globalization.CultureInfo.InvariantCulture),
        };
}
