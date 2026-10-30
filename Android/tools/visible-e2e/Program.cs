using System.Text.Json;
using Microsoft.Playwright;

// Visible browser driver for the local end-to-end walkthrough. Commands arrive as JSON lines.
// Uses a separate browser context and never attaches to the user's existing browser session.
var artifactRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../app/build/visible-e2e"));
Directory.CreateDirectory(artifactRoot);
using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync(new()
{
    Channel = "msedge", Headless = false, SlowMo = 650,
    Args = new[] { "--window-size=1100,900", "--window-position=0,0" }
});
await using var context = await browser.NewContextAsync(new() { ViewportSize = null, Locale = "es-AR" });
var page = await context.NewPageAsync();
page.SetDefaultTimeout(15000);
page.Dialog += async (_, dialog) => await dialog.AcceptAsync();
var responses = new System.Collections.Concurrent.ConcurrentQueue<object>();
page.Response += async (_, response) =>
{
    if (!response.Url.Contains("/api/")) return;
    try { responses.Enqueue(new { url = response.Url, status = response.Status, body = await response.TextAsync() }); }
    catch { }
};
await page.GotoAsync("http://localhost:5232/");
Console.WriteLine("READY: visible browser open; accepts JSON {steps:[...]} lines");
string? line;
while ((line = Console.ReadLine()) != null)
{
    try
    {
        using var command = JsonDocument.Parse(line);
        foreach (var step in command.RootElement.GetProperty("steps").EnumerateArray())
        {
            string Value(string key) => step.TryGetProperty(key, out var value) ? value.GetString() ?? "" : "";
            var action = Value("action");
            var selector = Value("selector");
            switch (action)
            {
                case "goto": await page.GotoAsync(Value("url")); break;
                case "click": await page.Locator(selector).ClickAsync(); break;
                case "fill": await page.Locator(selector).FillAsync(Value("value")); break;
                case "type":
                    await page.Locator(selector).FillAsync("");
                    await page.Locator(selector).PressSequentiallyAsync(Value("value"), new() { Delay = 65 });
                    break;
                case "check": await page.Locator(selector).CheckAsync(); break;
                case "select": await page.Locator(selector).SelectOptionAsync(Value("value")); break;
                case "reload": await page.ReloadAsync(); break;
                case "text": Console.WriteLine(await page.Locator(string.IsNullOrEmpty(selector) ? "body" : selector).InnerTextAsync()); break;
                case "controls":
                    Console.WriteLine(await page.Locator("input,select,button,textarea,a").EvaluateAllAsync<string>(
                        "els => JSON.stringify(els.filter(e => e.getClientRects().length).map(e => ({tag:e.tagName,id:e.id,type:e.type,text:(e.innerText||e.getAttribute('aria-label')||'').slice(0,120)})))"));
                    break;
                case "responses": while (responses.TryDequeue(out var response)) Console.WriteLine(JsonSerializer.Serialize(response)); break;
                case "screenshot":
                    var path = Path.Combine(artifactRoot, Path.GetFileName(Value("name")) + ".png");
                    await page.ScreenshotAsync(new() { Path = path, FullPage = true });
                    Console.WriteLine(path); break;
                case "front": await page.BringToFrontAsync(); break;
                case "wait": await Task.Delay(Math.Min(step.GetProperty("ms").GetInt32(), 10000)); break;
                default: throw new ArgumentException("Unknown action: " + action);
            }
            Console.WriteLine("OK: " + action + (selector.Length > 0 ? " " + selector : ""));
        }
        Console.WriteLine("DONE");
    }
    catch (Exception error) { Console.WriteLine("ERROR: " + error.Message); }
}
