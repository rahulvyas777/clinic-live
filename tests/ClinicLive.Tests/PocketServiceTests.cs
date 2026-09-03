using ClinicLive.Contracts;
using ClinicLive.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ClinicLive.Tests;

[Collection("postgres")]
public class PocketServiceTests(PostgresFixture fx)
{
    private QueueService Queue(IPushSender? push = null) => new(fx.DbFactory, new FakeQueueHub(), fx.ClinicTime, push ?? new FakePushSender());

    private PocketService NewService(IPushSender? push = null) =>
        new(fx.DbFactory, Queue(push), fx.ClinicTime, new ConfigurationBuilder().Build());

    [Fact]
    public async Task Calling_next_pushes_to_the_registered_phone_and_warns_whoever_is_next()
    {
        // Two patients late in the day so nothing else in the collection can be ahead of them.
        var booking = new BookingService(fx.DbFactory, fx.ClinicTime);
        var first = await booking.BookAsync("Test Patient M", "+00-1111-0013", null, DateTime.UtcNow.Date.AddHours(16));
        var second = await booking.BookAsync("Test Patient N", "+00-1111-0014", null, DateTime.UtcNow.Date.AddHours(16).AddMinutes(15));

        var push = new FakePushSender();
        var service = NewService(push);
        var queue = Queue(push);

        Assert.True(await service.RegisterDeviceAsync(first.Appointment!.ConfirmationCode, "android", "token-for-M"));
        Assert.True(await service.RegisterDeviceAsync(second.Appointment!.ConfirmationCode, "android", "token-for-N"));
        await queue.CheckInAsync(first.Appointment.ConfirmationCode);
        await queue.CheckInAsync(second.Appointment.ConfirmationCode);

        // Drain anyone earlier tests left waiting, then call M.
        while (true)
        {
            push.Sent.Clear();
            Assert.True(await queue.CallNextAsync());
            var serving = (await queue.GetSnapshotAsync()).NowServing;
            if (serving?.AppointmentId == first.Appointment.Id) break;
        }

        Assert.Contains(push.Sent, s => s.Title == "It's your turn" && s.Tokens.Contains("token-for-M") && s.Body.StartsWith("Test,"));
        Assert.Contains(push.Sent, s => s.Title == "You're next" && s.Tokens.Contains("token-for-N"));

        // Same phone, new appointment: the token MOVES (unique index), it isn't duplicated.
        Assert.True(await service.RegisterDeviceAsync(second.Appointment.ConfirmationCode, "android", "token-for-M"));
        await using var db = await fx.DbFactory.CreateDbContextAsync();
        Assert.Equal(1, await db.DeviceRegistrations.CountAsync(d => d.Token == "token-for-M"));
    }

    [Fact]
    public async Task Registering_a_device_for_an_unknown_code_is_refused()
    {
        Assert.False(await NewService().RegisterDeviceAsync("ZZZZZZ", "android", "some-token"));
    }

    [Fact]
    public async Task Unknown_code_is_null_not_an_exception()
    {
        Assert.Null(await NewService().GetVisitAsync("ZZZZZZ"));
    }

    [Fact]
    public async Task A_booking_for_today_can_check_in_and_shows_first_name_only()
    {
        var booking = await new BookingService(fx.DbFactory, fx.ClinicTime)
            .BookAsync("Test Patient J", "+00-1111-0010", null, DateTime.UtcNow.Date.AddHours(16).AddMinutes(30));

        var visit = await NewService().GetVisitAsync(booking.Appointment!.ConfirmationCode.ToLowerInvariant());

        Assert.NotNull(visit);
        Assert.Equal("Test", visit.FirstName);            // never the full name over the wire
        Assert.Equal(VisitStatus.Booked, visit.Status);
        Assert.True(visit.IsToday);
        Assert.True(visit.CanCheckIn);
        Assert.Null(visit.Position);
        Assert.Equal("Today", visit.DayLocal);
        Assert.Equal("16:30", visit.StartsAtLocal);
    }

    [Fact]
    public async Task Checking_in_through_the_api_lands_in_the_same_queue_as_the_kiosk()
    {
        var booking = await new BookingService(fx.DbFactory, fx.ClinicTime)
            .BookAsync("Test Patient K", "+00-1111-0011", null, DateTime.UtcNow.Date.AddHours(16).AddMinutes(45));
        var service = NewService();

        var checkIn = await service.CheckInAsync(booking.Appointment!.ConfirmationCode);
        var visit = await service.GetVisitAsync(booking.Appointment.ConfirmationCode);

        Assert.True(checkIn.Success);
        Assert.Equal(VisitStatus.CheckedIn, visit!.Status);
        Assert.False(visit.CanCheckIn);                    // once is enough
        Assert.NotNull(visit.Position);
        Assert.True(visit.Position >= 1);
        Assert.True(visit.WaitingCount >= 1);

        // The kiosk's own rule fires for a second attempt — one implementation, two doors.
        var again = await service.CheckInAsync(booking.Appointment.ConfirmationCode);
        Assert.False(again.Success);
        Assert.Contains("already checked in", again.Error);
    }

    [Fact]
    public async Task Tomorrows_booking_is_visible_but_cannot_check_in_yet()
    {
        // 13:15, not 09:00: the fixture is ONE database shared by every test in the
        // collection, and Season 1's booking test already owns tomorrow's 09:00 slot.
        // The partial unique index doesn't care which test asked first.
        var booking = await new BookingService(fx.DbFactory, fx.ClinicTime)
            .BookAsync("Test Patient L", "+00-1111-0012", null, DateTime.UtcNow.Date.AddDays(1).AddHours(13).AddMinutes(15));

        var visit = await NewService().GetVisitAsync(booking.Appointment!.ConfirmationCode);

        Assert.Equal("Tomorrow", visit!.DayLocal);
        Assert.False(visit.IsToday);
        Assert.False(visit.CanCheckIn);
    }
}
