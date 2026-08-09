using ClinicLive.Data;
using ClinicLive.Domain;
using ClinicLive.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ClinicLive.Services;

public sealed record QueueItem(long AppointmentId, string DisplayName, DateTime SlotAt, DateTime CheckedInAt);

public sealed record QueueSnapshot(QueueItem? NowServing, List<QueueItem> Waiting);

public sealed record CheckInResult(bool Success, string? Error = null, int Position = 0);

public class QueueService(IDbContextFactory<ApplicationDbContext> dbFactory, IHubContext<QueueHub> hub, ClinicTime clinic)
{
    public async Task<CheckInResult> CheckInAsync(string confirmationCode)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        // "Today" is the clinic's day, not the server's (Part 10's timezone fix).
        var (dayStart, dayEnd) = clinic.DayBoundsUtc(clinic.Today);
        var appointment = await db.Appointments
            .Include(a => a.QueueEntry)
            .FirstOrDefaultAsync(a => a.ConfirmationCode == confirmationCode.Trim().ToUpper()
                                   && a.StartsAt >= dayStart && a.StartsAt < dayEnd);

        if (appointment is null)
        {
            return new CheckInResult(false, "No appointment found for that code today.");
        }
        if (appointment.QueueEntry is not null)
        {
            return new CheckInResult(false, "You're already checked in — take a seat!");
        }
        if (appointment.Status is AppointmentStatus.Cancelled or AppointmentStatus.Done)
        {
            return new CheckInResult(false, $"This appointment is {appointment.Status}.");
        }

        appointment.Status = AppointmentStatus.CheckedIn;
        db.QueueEntries.Add(new QueueEntry { AppointmentId = appointment.Id });
        await db.SaveChangesAsync();

        await BroadcastChangeAsync();

        var snapshot = await GetSnapshotAsync();
        var position = snapshot.Waiting.FindIndex(w => w.AppointmentId == appointment.Id) + 1;
        return new CheckInResult(true, Position: position);
    }

    public async Task<QueueSnapshot> GetSnapshotAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var entries = await db.QueueEntries
            .Include(q => q.Appointment).ThenInclude(a => a.Patient)
            .Where(q => q.Appointment.Status == AppointmentStatus.CheckedIn
                     || q.Appointment.Status == AppointmentStatus.InProgress)
            .ToListAsync();

        var nowServing = entries
            .Where(q => q.Appointment.Status == AppointmentStatus.InProgress)
            .OrderByDescending(q => q.CalledAt)
            .Select(ToItem)
            .FirstOrDefault();

        // Spec: slot time first, check-in time as tiebreaker. Checking in early
        // doesn't let you jump ahead of an earlier appointment (docs/spec.md).
        var waiting = entries
            .Where(q => q.CalledAt == null)
            .OrderBy(q => q.Appointment.StartsAt)
            .ThenBy(q => q.CheckedInAt)
            .Select(ToItem)
            .ToList();

        return new QueueSnapshot(nowServing, waiting);
    }

    public async Task<bool> CallNextAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        // Whoever is currently in progress is done being served when the next is called.
        var inProgress = await db.QueueEntries
            .Include(q => q.Appointment)
            .Where(q => q.Appointment.Status == AppointmentStatus.InProgress)
            .ToListAsync();
        foreach (var entry in inProgress)
        {
            entry.Appointment.Status = AppointmentStatus.Done;
        }

        var next = await db.QueueEntries
            .Include(q => q.Appointment)
            .Where(q => q.CalledAt == null && q.Appointment.Status == AppointmentStatus.CheckedIn)
            .OrderBy(q => q.Appointment.StartsAt)
            .ThenBy(q => q.CheckedInAt)
            .FirstOrDefaultAsync();

        if (next is null)
        {
            await db.SaveChangesAsync();
            await BroadcastChangeAsync();
            return false;
        }

        next.CalledAt = DateTime.UtcNow;
        next.Appointment.Status = AppointmentStatus.InProgress;
        await db.SaveChangesAsync();

        await BroadcastChangeAsync();
        return true;
    }

    private Task BroadcastChangeAsync() =>
        hub.Clients.Group(QueueHub.BoardGroup).SendAsync("QueueChanged");

    /// <summary>The waiting-room board is public — first name and last initial only.</summary>
    private static QueueItem ToItem(QueueEntry q)
    {
        var parts = q.Appointment.Patient.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var display = parts.Length > 1 ? $"{parts[0]} {parts[^1][0]}." : parts[0];
        return new QueueItem(q.AppointmentId, display, q.Appointment.StartsAt, q.CheckedInAt);
    }
}
