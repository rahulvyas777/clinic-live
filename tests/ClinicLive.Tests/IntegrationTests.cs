using ClinicLive.Domain;
using ClinicLive.Services;
using Microsoft.EntityFrameworkCore;

namespace ClinicLive.Tests;

[Collection("postgres")]
public class BookingServiceTests(PostgresFixture fx)
{
    private BookingService NewService() => new(fx.DbFactory);

    private static DateTime Slot(int daysAhead, int hour, int minute) =>
        DateTime.UtcNow.Date.AddDays(daysAhead).AddHours(hour).AddMinutes(minute);

    [Fact]
    public async Task Booking_creates_patient_and_code()
    {
        var result = await NewService().BookAsync("Test Patient A", "+00-1111-0001", null, Slot(1, 9, 0));

        Assert.True(result.Success);
        Assert.Equal(6, result.Appointment!.ConfirmationCode.Length);

        await using var db = await fx.DbFactory.CreateDbContextAsync();
        Assert.NotNull(await db.Patients.FirstOrDefaultAsync(p => p.Phone == "+00-1111-0001"));
    }

    [Fact]
    public async Task Double_booking_the_same_slot_fails_gracefully()
    {
        var service = NewService();
        var slot = Slot(2, 10, 0);

        var first = await service.BookAsync("Test Patient B", "+00-1111-0002", null, slot);
        var second = await service.BookAsync("Test Patient C", "+00-1111-0003", null, slot);

        Assert.True(first.Success);
        Assert.False(second.Success);           // the partial unique index fired
        Assert.Contains("just taken", second.Error);
    }

    [Fact]
    public async Task Cancelling_frees_the_slot_for_rebooking()
    {
        var service = NewService();
        var slot = Slot(3, 11, 0);

        var original = await service.BookAsync("Test Patient D", "+00-1111-0004", null, slot);
        Assert.True(await service.CancelAsync(original.Appointment!.ConfirmationCode));

        var rebooked = await service.BookAsync("Test Patient E", "+00-1111-0005", null, slot);
        Assert.True(rebooked.Success);          // Cancelled rows are outside the index filter
    }
}

[Collection("postgres")]
public class QueueServiceTests(PostgresFixture fx)
{
    private QueueService NewService() => new(fx.DbFactory, new FakeQueueHub());

    [Fact]
    public async Task Check_in_marks_the_appointment_and_joins_the_queue()
    {
        var booking = await new BookingService(fx.DbFactory)
            .BookAsync("Test Patient F", "+00-1111-0006", null, DateTime.UtcNow.Date.AddHours(14));

        var result = await NewService().CheckInAsync(booking.Appointment!.ConfirmationCode);

        Assert.True(result.Success);
        await using var db = await fx.DbFactory.CreateDbContextAsync();
        var appointment = await db.Appointments
            .Include(a => a.QueueEntry)
            .SingleAsync(a => a.Id == booking.Appointment.Id);
        Assert.Equal(AppointmentStatus.CheckedIn, appointment.Status);
        Assert.NotNull(appointment.QueueEntry);
    }

    [Fact]
    public async Task Waiting_list_is_ordered_by_check_in_time()
    {
        // NOTE (Part 10 will revisit this test): it asserts what the CODE does today.
        var booking = new BookingService(fx.DbFactory);
        var queue = NewService();

        var lateSlot = await booking.BookAsync("Test Patient G", "+00-1111-0007", null, DateTime.UtcNow.Date.AddHours(15).AddMinutes(45));
        var earlySlot = await booking.BookAsync("Test Patient H", "+00-1111-0008", null, DateTime.UtcNow.Date.AddHours(15));

        // The late-slot patient checks in FIRST.
        await queue.CheckInAsync(lateSlot.Appointment!.ConfirmationCode);
        await queue.CheckInAsync(earlySlot.Appointment!.ConfirmationCode);

        var snapshot = await queue.GetSnapshotAsync();
        var names = snapshot.Waiting.Select(w => w.DisplayName).ToList();

        var lateIndex = names.IndexOf("Test G.");   // board masks to "First L."
        var earlyIndex = names.IndexOf("Test H.");
        Assert.True(lateIndex >= 0 && earlyIndex >= 0, "both patients should be waiting");
        Assert.True(lateIndex < earlyIndex, "first to check in is first in the queue");
    }
}
