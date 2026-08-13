using Microsoft.Playwright;

// Screenshot harness for the ClinicLive redesign series.
// Usage: dotnet run -- <outputDir> [baseUrl] [--checkin CODE] [--chat "message"]
// Captures every surface at its natural device size; logs in for staff pages.

var outDir = args.Length > 0 ? args[0] : "shots-out";
var baseUrl = args.Length > 1 && !args[1].StartsWith("--") ? args[1] : "http://localhost:5391";
string? checkinCode = null;
string? chatMessage = null;
var callNext = args.Contains("--callnext");
for (var i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--checkin") checkinCode = args[i + 1];
    if (args[i] == "--chat") chatMessage = args[i + 1];
}

Directory.CreateDirectory(outDir);

using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync();

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
        await kiosk.FillAsync("input.form-control-lg", code);
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

Console.WriteLine($"done → {Path.GetFullPath(outDir)}");
