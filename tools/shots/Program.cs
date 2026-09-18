using Microsoft.Playwright;

// Screenshot harness for the ClinicLive redesign series.
// Usage: dotnet run -- <outputDir> [baseUrl] [--checkin CODE] [--chat "message"] [--assistant "question"]
//                                  [--kiosk-ask "question"] [--pocket [--pocket-ask "question"]]
// Captures every surface at its natural device size; logs in for staff pages.

var outDir = args.Length > 0 ? args[0] : "shots-out";
var baseUrl = args.Length > 1 && !args[1].StartsWith("--") ? args[1] : "http://localhost:5391";
string? checkinCode = null;
string? chatMessage = null;
string? assistantQuestion = null;
string? kioskQuestion = null;
string? pocketQuestion = null;
var callNext = args.Contains("--callnext");
for (var i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--checkin") checkinCode = args[i + 1];
    if (args[i] == "--chat") chatMessage = args[i + 1];
    if (args[i] == "--assistant") assistantQuestion = args[i + 1];
    if (args[i] == "--kiosk-ask") kioskQuestion = args[i + 1];
    if (args[i] == "--pocket-ask") pocketQuestion = args[i + 1];
}

Directory.CreateDirectory(outDir);

// Season four: a local model has no "finished" event in the DOM, so the harness watches the
// text instead — when the element's text has not changed for 1.5 s the stream has stopped.
// A 14B model on a warm card takes 10–40 s to write an answer, so the ceiling is generous.
static async Task<string> SettleAsync(IPage page, string selector, int ceilingMs = 90_000)
{
    var lastText = "";
    var stableFor = 0;
    for (var waited = 0; waited < ceilingMs; waited += 300)
    {
        await page.WaitForTimeoutAsync(300);
        var element = await page.QuerySelectorAsync(selector);
        var text = element is null ? "" : (await element.InnerTextAsync()).Trim();

        if (text.Length > 0 && text == lastText)
        {
            stableFor += 300;
            if (stableFor >= 1500) break;
        }
        else
        {
            stableFor = 0;
            lastText = text;
        }
    }

    return lastText;
}

// The whole point of a smoke run is reading what the model actually said, not a character count.
static async Task ReportAsync(IPage page, string answer, string citeSelector)
{
    Console.WriteLine($"  answer ({answer.Length} chars):");
    Console.WriteLine("  ---");
    foreach (var line in answer.Replace("\r\n", "\n").Split('\n'))
    {
        Console.WriteLine("  " + line);
    }

    Console.WriteLine("  ---");
    foreach (var cite in await page.QuerySelectorAllAsync(citeSelector))
    {
        Console.WriteLine("  cite: " + (await cite.InnerTextAsync()).Trim());
    }
}

using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync();

// --pocket [--dark]: photograph the Pocket app's WEB host (season three) at phone size
// and stop. Same components as the Android/Windows shots, third host.
if (args.Contains("--pocket"))
{
    var scheme = args.Contains("--dark") ? ColorScheme.Dark : ColorScheme.Light;
    var pocket = await browser.NewContextAsync(new()
    {
        ViewportSize = new() { Width = 375, Height = 812 },
        ColorScheme = scheme,
    });
    Console.WriteLine($"pocket web host ({scheme}):");
    foreach (var (path, name) in new[] { ("/", "home"), ("/settings", "settings"), ("/visit/DEMO00", "visit"), ("/directions", "directions"), ("/queue", "queue") })
    {
        var page = await pocket.NewPageAsync();
        await page.GotoAsync($"{baseUrl}{path}", new() { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForTimeoutAsync(800);
        var file = $"web-{name}{(scheme == ColorScheme.Dark ? "-dark" : "")}.png";
        await page.ScreenshotAsync(new() { Path = Path.Combine(outDir, file) });
        Console.WriteLine($"  {file}");
        await page.CloseAsync();
    }

    // Part 10: the same components past the 900px breakpoint — the desk layout.
    var wide = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 800 }, ColorScheme = scheme });
    var wp = await wide.NewPageAsync();
    await wp.GotoAsync($"{baseUrl}/queue", new() { WaitUntil = WaitUntilState.NetworkIdle });
    await wp.WaitForTimeoutAsync(800);
    var wideFile = $"web-queue-desktop{(scheme == ColorScheme.Dark ? "-dark" : "")}.png";
    await wp.ScreenshotAsync(new() { Path = Path.Combine(outDir, wideFile) });
    Console.WriteLine($"  {wideFile}");

    // Season four, Part 9: ask the clinic from the app. The web host calls the clinic's API
    // server-to-server, so this is the real /api/pocket/assistant round trip — rate limiter,
    // public documents and all — photographed on a phone.
    if (pocketQuestion is not null)
    {
        Console.WriteLine($"asking from the app: {pocketQuestion}");
        var askPage = await pocket.NewPageAsync();
        await askPage.GotoAsync($"{baseUrl}/ask", new() { WaitUntil = WaitUntilState.NetworkIdle });
        await askPage.WaitForTimeoutAsync(800);   // let the circuit connect before typing
        await askPage.FillAsync(".ask-input", pocketQuestion);
        await askPage.ClickAsync("button[type='submit']");

        var answer = await SettleAsync(askPage, ".ask-answer");
        await ReportAsync(askPage, answer, ".ask-cite");

        await askPage.ScreenshotAsync(new() { Path = Path.Combine(outDir, "pocket-ask.png") });
        Console.WriteLine("  pocket-ask.png");
        await askPage.CloseAsync();
    }

    return;
}

// --kiosk-ask "question" — season four, Part 9. The kiosk's own panel, on the tablet it runs
// on. No login and no HTTP hop: the page calls the assistant in-process, as the public.
if (kioskQuestion is not null)
{
    var kioskCtx = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 768, Height = 1024 } });
    var kp = await kioskCtx.NewPageAsync();
    Console.WriteLine($"asking at the kiosk: {kioskQuestion}");
    await kp.GotoAsync($"{baseUrl}/kiosk", new() { WaitUntil = WaitUntilState.NetworkIdle });
    await kp.WaitForTimeoutAsync(800);   // let the circuit connect before typing
    await kp.FillAsync(".kiosk-ask-input", kioskQuestion);
    await kp.ClickAsync(".kiosk-ask-send");

    var kioskAnswer = await SettleAsync(kp, ".kiosk-answer-text");
    await ReportAsync(kp, kioskAnswer, ".kiosk-cite");

    await kp.ScreenshotAsync(new() { Path = Path.Combine(outDir, "kiosk-ask.png") });
    Console.WriteLine("  kiosk-ask.png");
    await kp.CloseAsync();
    return;
}

// --- public surfaces, each at its natural device ---
var phone = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 375, Height = 812 } });
var tablet = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 768, Height = 1024 } });
var desktop = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 800 } });

async Task Shot(IBrowserContext ctx, string path, string name, bool fullPage = false)
{
    var page = await ctx.NewPageAsync();
    await page.GotoAsync($"{baseUrl}{path}", new() { WaitUntil = WaitUntilState.NetworkIdle });
    await page.WaitForTimeoutAsync(600); // let Blazor circuits settle
    await page.ScreenshotAsync(new() { Path = Path.Combine(outDir, $"{name}.png"), FullPage = fullPage });
    Console.WriteLine($"  {name}.png");
    await page.CloseAsync();
}

Console.WriteLine("public surfaces:");
await Shot(desktop, "/", "home-desktop");
await Shot(phone, "/book", "book-phone", fullPage: true);

// --book "Full Name,+00-0000-0009" — complete a real booking and photograph the ticket
var bookArg = Array.IndexOf(args, "--book") is var bi and >= 0 && bi < args.Length - 1 ? args[bi + 1] : null;
if (bookArg is not null)
{
    var (name, phoneNo) = (bookArg.Split(',')[0], bookArg.Split(',')[1]);
    Console.WriteLine($"booking for {name}:");
    var bp = await phone.NewPageAsync();
    await bp.GotoAsync($"{baseUrl}/book", new() { WaitUntil = WaitUntilState.NetworkIdle });
    await bp.WaitForTimeoutAsync(600);
    await bp.ClickAsync(".slot-btn >> nth=-1");           // last free slot of the day
    await bp.FillAsync("#name", name);
    await bp.FillAsync("#phone", phoneNo);
    await bp.ClickAsync("button[type='submit']");
    await bp.WaitForSelectorAsync(".ticket");
    await bp.WaitForTimeoutAsync(900);   // let the pop-in finish and the QR image (season three) load
    await bp.ScreenshotAsync(new() { Path = Path.Combine(outDir, "book-phone-ticket.png"), FullPage = true });
    Console.WriteLine("  book-phone-ticket.png");
    await bp.CloseAsync();
}

await Shot(phone, "/cancel", "cancel-phone");
await Shot(tablet, "/kiosk", "kiosk-tablet");

// optional: check a patient in so the board/queue have life in them
if (checkinCode is not null)
{
    var kiosk = await tablet.NewPageAsync();
    await kiosk.GotoAsync($"{baseUrl}/kiosk", new() { WaitUntil = WaitUntilState.NetworkIdle });
    foreach (var code in checkinCode.Split(','))
    {
        Console.WriteLine($"checking in {code}:");
        await kiosk.FillAsync(".kiosk-code, input.form-control-lg", code);
        await kiosk.ClickAsync("button:has-text('Check in')");
        await kiosk.WaitForTimeoutAsync(800);
    }
    await kiosk.ScreenshotAsync(new() { Path = Path.Combine(outDir, "kiosk-tablet-checkedin.png") });
    Console.WriteLine("  kiosk-tablet-checkedin.png");
    await kiosk.CloseAsync();
}

await Shot(desktop, "/board", "board-tv");

// --- staff surfaces, behind login ---
Console.WriteLine("staff login:");
var staff = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 800 } });
var login = await staff.NewPageAsync();
await login.GotoAsync($"{baseUrl}/Account/Login", new() { WaitUntil = WaitUntilState.NetworkIdle });
await login.ScreenshotAsync(new() { Path = Path.Combine(outDir, "login-desktop.png") });
Console.WriteLine("  login-desktop.png");
await login.FillAsync("input[name='Input.Email']", "reception@cliniclive.test");
await login.FillAsync("input[name='Input.Password']", "Clinic!Live1");
await login.ClickAsync("button[type='submit']");
await login.WaitForURLAsync("**/", new() { Timeout = 15000 });
await login.CloseAsync();

if (callNext)
{
    Console.WriteLine("calling next patient:");
    var q = await staff.NewPageAsync();
    await q.GotoAsync($"{baseUrl}/staff/queue", new() { WaitUntil = WaitUntilState.NetworkIdle });
    await q.WaitForTimeoutAsync(600);
    await q.ClickAsync("button:has-text('Call next')");
    await q.WaitForTimeoutAsync(800);
    await q.CloseAsync();
}

Console.WriteLine("staff surfaces:");
await Shot(staff, "/staff/appointments", "staff-appointments");
await Shot(staff, "/staff/queue", "staff-queue");

if (chatMessage is not null)
{
    var chat = await staff.NewPageAsync();
    await chat.GotoAsync($"{baseUrl}/staff/chat", new() { WaitUntil = WaitUntilState.NetworkIdle });
    await chat.WaitForTimeoutAsync(600);
    await chat.FillAsync("input[placeholder*='Message']", chatMessage);
    await chat.PressAsync("input[placeholder*='Message']", "Enter");
    await chat.WaitForTimeoutAsync(600);
    await chat.ScreenshotAsync(new() { Path = Path.Combine(outDir, "staff-chat.png") });
    Console.WriteLine("  staff-chat.png");
    await chat.CloseAsync();
}
else
{
    await Shot(staff, "/staff/chat", "staff-chat");
}

// --assistant "question" — season four, Part 5. Ask the local model and wait for it to
// finish. There is no "done" signal in the DOM, so we watch the last bubble: when its text
// has not changed for 1.5 s the stream has stopped. A 14B model on a warm card takes
// 10–40 s, so the ceiling is generous.
if (assistantQuestion is not null)
{
    Console.WriteLine($"asking the assistant: {assistantQuestion}");
    var ap = await staff.NewPageAsync();
    await ap.GotoAsync($"{baseUrl}/staff/assistant", new() { WaitUntil = WaitUntilState.NetworkIdle });
    await ap.WaitForTimeoutAsync(600);   // let the circuit connect before typing
    await ap.FillAsync("input[placeholder*='Ask the assistant']", assistantQuestion);
    await ap.PressAsync("input[placeholder*='Ask the assistant']", "Enter");

    var lastText = "";
    var stableFor = 0;
    for (var waited = 0; waited < 90_000; waited += 300)
    {
        await ap.WaitForTimeoutAsync(300);
        var bubbles = await ap.QuerySelectorAllAsync(".chat-bubble");
        var text = bubbles.Count > 1 ? await bubbles[^1].InnerTextAsync() : "";
        if (text.Length > 0 && text == lastText)
        {
            stableFor += 300;
            if (stableFor >= 1500) break;
        }
        else
        {
            stableFor = 0;
            lastText = text;
        }
    }

    // Part 7: print the answer and its citations, not just a character count — the whole
    // point of this run is reading what the model actually said and which documents it used.
    Console.WriteLine($"  answer ({lastText.Length} chars):");
    Console.WriteLine("  ---");
    foreach (var line in lastText.Replace("\r\n", "\n").Split('\n'))
    {
        Console.WriteLine("  " + line);
    }

    Console.WriteLine("  ---");
    foreach (var cite in await ap.QuerySelectorAllAsync(".chat-cite"))
    {
        Console.WriteLine("  cite: " + (await cite.InnerTextAsync()).Trim());
    }

    await ap.ScreenshotAsync(new() { Path = Path.Combine(outDir, "staff-assistant.png") });
    Console.WriteLine("  staff-assistant.png");
    await ap.CloseAsync();
}

Console.WriteLine($"done → {Path.GetFullPath(outDir)}");
