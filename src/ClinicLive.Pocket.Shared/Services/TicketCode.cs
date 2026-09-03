using System.Text.RegularExpressions;

namespace ClinicLive.Pocket.Shared.Services;

/// <summary>
/// Pulls a confirmation code out of whatever a camera or a keyboard produced:
/// "cliniclive://visit/ABC123", a bare "abc123", a pasted URL with the code in it.
/// The alphabet is the clinic's (no 0/O/1/I/L), so a random QR from a cereal box
/// simply doesn't match.
/// </summary>
public static partial class TicketCode
{
    // The alphabet is ABCDEFGHJKMNPQRSTUVWXYZ23456789. The first draft wrote the class as
    // [A-HJ-NP-Z2-9] — and J-N spans J,K,L,M,N, quietly letting L back in. The test that
    // feeds it a cereal-box URL ("cereal" = six letters) went red and caught it.
    [GeneratedRegex("(?<![A-Z0-9])([A-HJ-KM-NP-Z2-9]{6})(?![A-Z0-9])", RegexOptions.IgnoreCase)]
    private static partial Regex CodePattern();

    public static bool TryParse(string? raw, out string code)
    {
        code = "";
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var match = CodePattern().Match(raw.Trim());
        if (!match.Success)
        {
            return false;
        }

        code = match.Groups[1].Value.ToUpperInvariant();
        return true;
    }
}
