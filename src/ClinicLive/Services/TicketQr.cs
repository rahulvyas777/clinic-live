using QRCoder;

namespace ClinicLive.Services;

/// <summary>
/// The QR on a booking ticket. Payload is a tiny URI, cliniclive://visit/ABC123, so
/// (a) the Pocket app can recognise its own tickets and (b) a generic QR reader
/// shows something self-explanatory rather than six bare letters.
/// </summary>
public static class TicketQr
{
    public const string Scheme = "cliniclive://visit/";

    public static string Payload(string code) => Scheme + code;

    public static byte[] Png(string code, int pixelsPerModule = 8)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(Payload(code), QRCodeGenerator.ECCLevel.M);
        using var png = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule);
    }
}
