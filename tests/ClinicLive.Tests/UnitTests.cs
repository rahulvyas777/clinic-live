using ClinicLive.Services;

namespace ClinicLive.Tests;

public class ConfirmationCodeTests
{
    [Fact]
    public void Codes_are_six_chars_from_the_unambiguous_alphabet()
    {
        // No 0/O, 1/I/L — codes get read aloud and typed on a kiosk.
        const string alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

        for (var i = 0; i < 200; i++)
        {
            var code = ConfirmationCode.NewCode();
            Assert.Equal(6, code.Length);
            Assert.All(code, c => Assert.Contains(c, alphabet));
        }
    }

    [Fact]
    public void Codes_vary()
    {
        var codes = Enumerable.Range(0, 50).Select(_ => ConfirmationCode.NewCode()).ToHashSet();
        Assert.True(codes.Count > 45, "200M+ combinations shouldn't collide this often");
    }
}

public class SlotTests
{
    [Fact]
    public void A_day_has_32_slots_from_0900_to_1645()
    {
        var slots = BookingService.AllSlotsFor(new DateOnly(2026, 8, 10)).ToList();

        Assert.Equal(32, slots.Count);                       // 8 hours x 4 slots
        Assert.Equal(new TimeOnly(9, 0), TimeOnly.FromDateTime(slots[0]));
        Assert.Equal(new TimeOnly(16, 45), TimeOnly.FromDateTime(slots[^1]));
        Assert.All(slots, s => Assert.Equal(0, s.Minute % 15));
        Assert.All(slots, s => Assert.Equal(DateTimeKind.Utc, s.Kind));
    }
}
