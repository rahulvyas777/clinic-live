using System.Text;
using System.Threading.RateLimiting;
using ClinicLive.Contracts;
using ClinicLive.Domain;
using ClinicLive.Services;
using ClinicLive.Services.Ai;
using Microsoft.AspNetCore.RateLimiting;

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

        // Season four, Part 9: the patient assistant. Same pipeline as the staff one, three
        // things taken away — the staff documents, the tools, and the right to ask all day.
        api.MapPost("/assistant", AskAsync)
           .RequireRateLimiting(AssistantPolicy);

        return app;
    }

    // ---------------------------------------------------------------------------------------
    // the patient assistant
    // ---------------------------------------------------------------------------------------

    /// <summary>The rate-limiter policy this endpoint requires. Named, so the pipeline can find it.</summary>
    public const string AssistantPolicy = "assistant";

    /// <summary>Questions allowed per client per window. A curious patient never notices it.</summary>
    public const int AssistantPermitLimit = 10;

    /// <summary>The window those permits refill in.</summary>
    public static readonly TimeSpan AssistantWindow = TimeSpan.FromMinutes(1);

    /// <summary>
    /// The limiter behind the "assistant" policy, as its own method so a test can read the
    /// numbers instead of trusting a lambda buried in startup.
    /// <para>
    /// Fixed window rather than token bucket because the thing being protected is a GPU, not a
    /// database: ten questions a minute is about the most one clinic's card can answer anyway,
    /// and a queue would only hide that behind a spinner. QueueLimit 0 — over the limit is a
    /// 429 now, not a wait for a machine that is already busy.
    /// </para>
    /// </summary>
    public static FixedWindowRateLimiterOptions AssistantLimiterOptions() => new()
    {
        PermitLimit = AssistantPermitLimit,
        Window = AssistantWindow,
        QueueLimit = 0,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        AutoReplenishment = true,
    };

    /// <summary>
    /// Register the assistant's rate limiter. One policy, partitioned by client IP: the kiosk is
    /// one client, every phone is another. Behind nginx this sees the proxy unless forwarded
    /// headers are configured (deploy notes) — which is the honest place to fix it, not here.
    /// </summary>
    public static IServiceCollection AddAssistantRateLimiter(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(AssistantPolicy, http =>
                RateLimitPartition.GetFixedWindowLimiter(ClientKey(http), _ => AssistantLimiterOptions()));

            // The default rejection is an empty 429; the app on the other end needs a sentence
            // it can show, so this writes the same short JSON every other error here uses.
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { error = AssistantContract.TooManyError }, ct);
            };
        });

    /// <summary>Who is asking, as far as an unauthenticated endpoint can tell: their address.</summary>
    private static string ClientKey(HttpContext http) =>
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    /// <summary>
    /// Ask the clinic's own model a patient's question and stream the answer back as it is
    /// written. Plain text, not JSON: a JSON body cannot be half-parsed, and the whole point of
    /// a local model is that the first words appear long before the last ones.
    /// <para>
    /// Two things are never negotiable here. The audience is "public", written in this method
    /// and never read from the request — see <see cref="AssistantQuestion"/>. And no tool is
    /// ever offered: <see cref="AssistantService"/> makes that an audience test of its own, so
    /// this endpoint could not hand one over even by mistake.
    /// </para>
    /// </summary>
    private static async Task<IResult> AskAsync(
        AssistantQuestion request,
        AssistantService assistant,
        HttpContext http,
        CancellationToken ct)
    {
        var question = request.Question?.Trim() ?? string.Empty;
        if (!AssistantContract.IsAskable(question))
        {
            return Results.BadRequest(new { error = AssistantContract.LengthError });
        }

        var reply = await assistant.AskAsync(question, KnowledgeAudience.Public, ct);

        // Headers first, while they can still be set: retrieval has already finished, so the
        // citations are known before the model has written a single token.
        http.Response.ContentType = "text/plain; charset=utf-8";
        if (CitationHeaderValue(reply.Chunks) is { Length: > 0 } citations)
        {
            http.Response.Headers[AssistantContract.CitationHeader] = citations;
        }

        await foreach (var delta in reply.Deltas.WithCancellation(ct))
        {
            if (delta == AssistantService.GuardMarker)
            {
                // The finished answer carried no citation, so it was never allowed to stand.
                // A stream cannot be unsent, so the refusal goes out last and the client shows
                // that instead of what came before it (PocketApi.AskAsync).
                await http.Response.WriteAsync(AssistantService.RefusalText, ct);
                break;
            }

            if (delta == AssistantService.ResetMarker)
            {
                // Only a tool run produces this, and this path never has one. Swallowed rather
                // than forwarded, so a marker can never be mistaken for an answer.
                continue;
            }

            await http.Response.WriteAsync(delta, ct);
            await http.Response.Body.FlushAsync(ct);
        }

        return Results.Empty;
    }

    /// <summary>
    /// The retrieved documents as one header value: "[1] Title &gt; Heading | [2] Title".
    /// Non-ASCII is dropped rather than encoded — a header is Latin-1 on the wire, and a
    /// citation is a document title, not content.
    /// </summary>
    private static string CitationHeaderValue(IReadOnlyList<RetrievedChunk> chunks)
    {
        if (chunks.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        for (var i = 0; i < chunks.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(AssistantContract.CitationSeparator);
            }

            sb.Append('[').Append(i + 1).Append("] ");
            foreach (var c in chunks[i].Label)
            {
                if (c is >= ' ' and <= '~' && c != '|')
                {
                    sb.Append(c);
                }
            }
        }

        return sb.ToString();
    }
}
