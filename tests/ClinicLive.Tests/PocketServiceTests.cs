using ClinicLive.Contracts;
using ClinicLive.Services;
using Microsoft.Extensions.Configuration;

namespace ClinicLive.Tests;

[Collection("postgres")]
public class PocketServiceTests(PostgresFixture fx)
{
    private QueueService Queue() => new(fx.DbFactory, new FakeQueueHub(), fx.ClinicTime);

    private PocketService NewService() =>
        new(fx.DbFactory, Queue(), fx.ClinicTime, new ConfigurationBuilder().Build());

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
