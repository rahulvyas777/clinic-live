using ClinicLive.Data;
using ClinicLive.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClinicLive.Services;

public sealed record BookingResult(bool Success, string? Error = null, Appointment? Appointment = null);

public class BookingService(IDbContextFactory<ApplicationDbContext> dbFactory, ClinicTime clinic)
{
    public const int OpeningHour = 9;   // clinic opens 09:00 local
    public const int ClosingHour = 17;  // last slot starts 16:45 local
    public const int SlotMinutes = 15;

    /// <summary>All bookable slot times (UTC) for a clinic-local date that are free and not in the past.</summary>
    public async Task<List<DateTime>> GetFreeSlotsAsync(DateOnly date)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var (dayStart, dayEnd) = clinic.DayBoundsUtc(date);
        var taken = await db.Appointments
            .Where(a => a.StartsAt >= dayStart
                     && a.StartsAt < dayEnd
                     && a.Status != AppointmentStatus.Cancelled
                     && a.Status != AppointmentStatus.NoShow)
            .Select(a => a.StartsAt)
            .ToListAsync();

        var takenSet = taken.ToHashSet();
        var now = DateTime.UtcNow;

        return AllSlotsFor(date, clinic.Zone)
            .Where(slot => !takenSet.Contains(slot) && slot > now)
            .ToList();
    }

    public async Task<BookingResult> BookAsync(string fullName, string phone, string? email, DateTime slotUtc)
    {
        slotUtc = DateTime.SpecifyKind(slotUtc, DateTimeKind.Utc);

        // Validate against the clinic's wall clock, not the server's.
        var local = TimeZoneInfo.ConvertTimeFromUtc(slotUtc, clinic.Zone);
        if (local.Minute % SlotMinutes != 0 || local.Hour < OpeningHour || local.Hour >= ClosingHour)
        {
            return new BookingResult(false, "That's not a valid slot time.");
        }

        await using var db = await dbFactory.CreateDbContextAsync();

        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Phone == phone.Trim())
            ?? new Patient { FullName = fullName.Trim(), Phone = phone.Trim(), Email = email?.Trim() };

        var appointment = new Appointment
        {
            Patient = patient,
            StartsAt = slotUtc,
            ConfirmationCode = ConfirmationCode.NewCode(),
        };
        db.Appointments.Add(appointment);

        try
        {
            await db.SaveChangesAsync();
            return new BookingResult(true, Appointment: appointment);
        }
        catch (DbUpdateException)
        {
            // The partial unique index on starts_at fired — someone beat us to the slot
            // (or, far less likely, a confirmation-code collision). Either way: retry-able.
            return new BookingResult(false, "Sorry — that slot was just taken. Please pick another.");
        }
    }

    public async Task<bool> CancelAsync(string confirmationCode)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var appointment = await db.Appointments
            .FirstOrDefaultAsync(a => a.ConfirmationCode == confirmationCode.Trim().ToUpper()
                                   && a.Status == AppointmentStatus.Booked);
        if (appointment is null)
        {
            return false;
        }

        appointment.Status = AppointmentStatus.Cancelled;
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>Every slot of a clinic-local day, as UTC instants.</summary>
    public static IEnumerable<DateTime> AllSlotsFor(DateOnly date, TimeZoneInfo zone)
    {
        var count = (ClosingHour - OpeningHour) * 60 / SlotMinutes;
        for (var i = 0; i < count; i++)
        {
            var wallTime = new TimeOnly(OpeningHour, 0).AddMinutes(i * SlotMinutes);
            yield return TimeZoneInfo.ConvertTimeToUtc(
                date.ToDateTime(wallTime, DateTimeKind.Unspecified), zone);
        }
    }
}
