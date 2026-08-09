namespace ClinicLive.Services;

public static class ConfirmationCode
{
    // No 0/O, 1/I/L — codes get read aloud and typed on a kiosk.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string NewCode(int length = 6) =>
        string.Create(length, Random.Shared, static (span, rng) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = Alphabet[rng.Next(Alphabet.Length)];
            }
        });
}
