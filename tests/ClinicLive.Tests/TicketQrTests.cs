using ClinicLive.Pocket.Shared.Services;
using ClinicLive.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ZXing;

namespace ClinicLive.Tests;

/// <summary>
/// The round trip the emulator couldn't stage for us: the server draws the QR, a real
/// decoder (ZXing, the same family the phone uses) reads it back, and the app's parser
/// pulls the code out. If any link in that chain drifts, this goes red before a
/// patient's ticket does.
/// </summary>
public class TicketQrTests
{
    [Fact]
    public void The_ticket_qr_decodes_back_to_its_code()
    {
        var png = TicketQr.Png("PWPAP2");

        using var image = Image.Load<Rgba32>(png);
        // Fully qualified: ZXing and ZXing.ImageSharp both define a BarcodeReader<T>.
        var reader = new ZXing.ImageSharp.BarcodeReader<Rgba32> { Options = { PossibleFormats = [BarcodeFormat.QR_CODE] } };
        var result = reader.Decode(image);

        Assert.NotNull(result);
        Assert.Equal("cliniclive://visit/PWPAP2", result.Text);
        Assert.True(TicketCode.TryParse(result.Text, out var code));
        Assert.Equal("PWPAP2", code);
    }

    [Theory]
    [InlineData("cliniclive://visit/ABC234", "ABC234")]
    [InlineData("abc234", "ABC234")]
    [InlineData("https://example.test/?code=XYZ789&x=1", "XYZ789")]
    public void Codes_are_found_in_whatever_the_camera_or_keyboard_produced(string raw, string expected)
    {
        Assert.True(TicketCode.TryParse(raw, out var code));
        Assert.Equal(expected, code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("hello world")]
    [InlineData("ABC1O0")]           // 1, O and 0 are not in the clinic's alphabet
    [InlineData("https://cereal.example/box-of-flakes")]
    public void Random_qrs_are_not_tickets(string raw)
    {
        Assert.False(TicketCode.TryParse(raw, out _));
    }
}
