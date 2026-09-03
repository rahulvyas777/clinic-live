using System.Net;
using System.Net.Http.Json;
using ClinicLive.Contracts;

namespace ClinicLive.Pocket.Shared.Services;

/// <summary>Thrown when the clinic can't be reached at all — the UI turns it into "check your connection".</summary>
public sealed class ClinicUnreachableException(Exception inner) : Exception("The clinic's server can't be reached.", inner);

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

    public Task<QueueDto?> GetQueueAsync(CancellationToken ct = default) =>
        Guard(() => http.GetFromJsonAsync<QueueDto>("api/pocket/queue", ct));

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
