using ClinicLive.Contracts;
using ClinicLive.Services;

namespace ClinicLive.Api;

/// <summary>
/// The Pocket app's door into the clinic: four minimal-API endpoints under /api/pocket.
/// No login — the confirmation code is the patient's credential, the same as at the kiosk.
/// No CORS either: neither the MAUI app nor the Blazor Server web host is a browser fetch.
/// </summary>
public static class PocketEndpoints
{
    public static IEndpointRouteBuilder MapPocketApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/pocket")
            .WithTags("Pocket")
            .DisableAntiforgery();   // JSON API, not a form post

        api.MapGet("/clinic", (PocketService pocket) => pocket.GetClinicInfo());

        api.MapGet("/visits/{code}", async (string code, PocketService pocket) =>
            await pocket.GetVisitAsync(code) is { } visit
                ? Results.Ok(visit)
                : Results.NotFound(new { error = "No visit found for that code." }));

        api.MapPost("/visits/{code}/check-in", async (string code, PocketService pocket) =>
        {
            var result = await pocket.CheckInAsync(code);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        });

        api.MapGet("/queue", (PocketService pocket) => pocket.GetQueueAsync());

        // Part 8: the ticket's QR code. It encodes the same six characters the patient
        // could type — a QR is a convenience, never a second credential. The image for
        // a code never changes, so the browser may keep it for a day.
        api.MapGet("/visits/{code}/qr.png", async (string code, PocketService pocket, HttpContext http) =>
        {
            if (await pocket.GetVisitAsync(code) is null)
            {
                return Results.NotFound();
            }
            http.Response.Headers.CacheControl = "public, max-age=86400";
            return Results.File(TicketQr.Png(code.Trim().ToUpperInvariant()), "image/png");
        });

        // Part 6: "this phone wants to hear about this visit".
        api.MapPost("/visits/{code}/device", async (string code, DeviceRegistrationRequest request, PocketService pocket) =>
            await pocket.RegisterDeviceAsync(code, request.Platform, request.Token)
                ? Results.NoContent()
                : Results.NotFound(new { error = "No visit found for that code." }));

        return app;
    }
}
