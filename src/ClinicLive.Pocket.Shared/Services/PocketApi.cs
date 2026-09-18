using System.Net;
using System.Net.Http.Json;
using System.Text;
using ClinicLive.Contracts;

namespace ClinicLive.Pocket.Shared.Services;

/// <summary>Thrown when the clinic can't be reached at all — the UI turns it into "check your connection".</summary>
public sealed class ClinicUnreachableException(Exception inner) : Exception("The clinic's server can't be reached.", inner);

/// <summary>
/// One answer from the clinic's assistant: the text to show, and the documents it stands on.
/// </summary>
/// <param name="Text">The finished answer, or the fixed refusal.</param>
/// <param name="Sources">Document titles, already filtered to the ones the answer cited.</param>
public sealed record AssistantAnswer(string Text, IReadOnlyList<string> Sources);

/// <summary>
/// The typed client for /api/pocket. Every host hands it an HttpClient whose BaseAddress
/// points at the clinic — the emulator, a desktop and a browser all get there differently,
/// and that difference is the host's problem, not this class's.
/// </summary>
public sealed class PocketApi(HttpClient http)
{
    public Task<ClinicInfo?> GetClinicAsync(CancellationToken ct = default) =>
        Guard(() => http.GetFromJsonAsync<ClinicInfo>("api/pocket/clinic", ct));

    /// <summary>Null means "no such code" (404). Anything else that goes wrong throws.</summary>
    public Task<VisitDto?> GetVisitAsync(string code, CancellationToken ct = default) =>
        Guard(async () =>
        {
            using var response = await http.GetAsync($"api/pocket/visits/{Uri.EscapeDataString(code.Trim())}", ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<VisitDto>(ct);
        });

    public Task<CheckInResponse> CheckInAsync(string code, CancellationToken ct = default) =>
        Guard(async () =>
        {
            // 200 and 400 both carry a CheckInResponse — the error text is part of the contract.
            using var response = await http.PostAsync($"api/pocket/visits/{Uri.EscapeDataString(code.Trim())}/check-in", null, ct);
            return await response.Content.ReadFromJsonAsync<CheckInResponse>(ct)
                   ?? new CheckInResponse(false, "The clinic sent an empty reply.", 0);
        })!;

    /// <summary>Attach this device's push token to a visit. False when the code is unknown.</summary>
    public Task<bool> RegisterDeviceAsync(string code, string platform, string token, CancellationToken ct = default) =>
        Guard(async () =>
        {
            using var response = await http.PostAsJsonAsync(
                $"api/pocket/visits/{Uri.EscapeDataString(code.Trim())}/device",
                new DeviceRegistrationRequest(platform, token), ct);
            return response.IsSuccessStatusCode;
        });

    public Task<QueueDto?> GetQueueAsync(CancellationToken ct = default) =>
        Guard(() => http.GetFromJsonAsync<QueueDto>("api/pocket/queue", ct));

    /// <summary>
    /// A question for the clinic's assistant (season four, Part 9). The reply is not JSON: it
    /// is the answer itself, arriving as the model writes it, with the citations in a header
    /// that lands before the first token does.
    /// <para>
    /// The clinic's own 14 B model takes tens of seconds to think, which is why this one call
    /// does not use the app's HttpClient: ten seconds is the right patience for "where is the
    /// clinic" and the wrong patience for "answer me a question". Everything else about the
    /// call is identical, base address included.
    /// </para>
    /// </summary>
    public Task<AssistantAnswer> AskAsync(string question, CancellationToken ct = default) =>
        Guard(async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/pocket/assistant")
            {
                Content = JsonContent.Create(new AssistantQuestion(question.Trim())),
            };

            using var response = await _thinking.Value.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, ct);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return new AssistantAnswer(AssistantContract.TooManyError, []);
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                return new AssistantAnswer(AssistantContract.LengthError, []);
            }

            response.EnsureSuccessStatusCode();

            var header = response.Headers.TryGetValues(AssistantContract.CitationHeader, out var values)
                ? string.Join(AssistantContract.CitationSeparator, values)
                : null;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            // Read it as it arrives rather than in one gulp: the server flushes every token, and
            // a ReadToEndAsync would sit on a socket for half a minute pretending nothing had.
            var answer = new StringBuilder();
            var buffer = new char[256];
            int read;
            while ((read = await reader.ReadAsync(buffer, ct)) > 0)
            {
                answer.Append(buffer, 0, read);
            }

            // What to show is the answer minus its "Sources:" line, and nothing but the refusal
            // if it ends with one. What to cite is decided from the WHOLE text, because the
            // numbers to match against the header live on exactly the line that was removed.
            var raw = answer.ToString();
            return new AssistantAnswer(
                AssistantContract.NormalizeAnswer(AssistantContract.WithoutSourcesLine(raw)),
                AssistantContract.Citations(raw, header));
        });

    /// <summary>
    /// The one client with a long fuse, built only if the assistant is ever asked anything.
    /// Same base address as the app's; two minutes instead of ten seconds.
    /// </summary>
    private readonly Lazy<HttpClient> _thinking = new(() => new HttpClient
    {
        BaseAddress = http.BaseAddress,
        Timeout = TimeSpan.FromMinutes(2),
    });

    private static async Task<T> Guard<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (HttpRequestException ex)
        {
            throw new ClinicUnreachableException(ex);
        }
        catch (TaskCanceledException ex) when (!ex.CancellationToken.IsCancellationRequested)
        {
            throw new ClinicUnreachableException(ex);   // timeout, not a user cancel
        }
    }
}
