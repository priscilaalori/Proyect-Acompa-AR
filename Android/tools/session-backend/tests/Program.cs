using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

var clock = new TestClock();
var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Warning);
builder.Services.AddSingleton<TimeProvider>(clock);
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.TimeProvider = clock;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
});
builder.Services.AddAuthorization();
await using var app = builder.Build();
app.UseAuthentication();
app.UseMiddleware<MobileSessionMiddleware>();
app.UseAuthorization();
app.MapPost("/api/auth/login", async (HttpContext context) =>
{
    var properties = MobileSessionPolicy.LoginProperties(context);
    var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.NameIdentifier, "123"), new Claim(ClaimTypes.Role, "MAYOR")
    }, CookieAuthenticationDefaults.AuthenticationScheme));
    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, user, properties);
    MobileSessionPolicy.WriteExpiration(context, properties);
    return Results.Ok(new { ok = true });
});
app.MapGet("/panel", () => Results.Ok(new { ok = true })).RequireAuthorization();
app.Urls.Add("http://127.0.0.1:0");
await app.StartAsync();
using var client = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false })
    { BaseAddress = new Uri(app.Urls.Single()) };
var initial = clock.GetUtcNow();
var checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    checks++;
    Console.WriteLine("PASS: " + name);
}
async Task<HttpResponseMessage> Send(string? cookie, bool mobile = true, DateTimeOffset? activity = null, bool login = false)
{
    var request = new HttpRequestMessage(login ? HttpMethod.Post : HttpMethod.Get, login ? "/api/auth/login" : "/panel");
    if (cookie != null) request.Headers.Add("Cookie", cookie);
    if (mobile) request.Headers.Add("X-Acompaar-Client", "android");
    if (activity != null) request.Headers.Add("X-Acompaar-Activity", activity.Value.ToUnixTimeMilliseconds().ToString());
    return await client.SendAsync(request);
}
string Cookie(HttpResponseMessage response) => response.Headers.GetValues("Set-Cookie").First().Split(';')[0];
long Expiry(HttpResponseMessage response) => long.Parse(response.Headers.GetValues(MobileSessionPolicy.ExpirationHeader).Single());

var login = await Send(null, login: true);
var cookie = Cookie(login);
Check(Expiry(login) == initial.AddDays(7).ToUnixTimeMilliseconds(), "mobile login lasts seven days");
clock.Now = initial.AddDays(3);
var passive = await Send(cookie);
Check(passive.IsSuccessStatusCode && !passive.Headers.Contains("Set-Cookie")
    && Expiry(passive) == initial.AddDays(7).ToUnixTimeMilliseconds(), "background polling cannot renew session");

clock.Now = initial.AddDays(4);
var opened = await Send(cookie, activity: clock.Now);
var renewedCookie = Cookie(opened);
Check(Expiry(opened) == initial.AddDays(11).ToUnixTimeMilliseconds(), "opening app starts a new seven-day window");
clock.Now = initial.AddDays(8);
Check((await Send(cookie, activity: clock.Now)).StatusCode == HttpStatusCode.Unauthorized,
    "activity cannot revive expired original cookie");
Check((await Send(renewedCookie)).IsSuccessStatusCode, "renewed cookie remains valid after original expiration");

var respondedAt = initial.AddDays(8.5);
clock.Now = initial.AddDays(9);
var response = await Send(renewedCookie, activity: respondedAt);
var responseCookie = Cookie(response);
Check(Expiry(response) == respondedAt.AddDays(7).ToUnixTimeMilliseconds(), "offline answer uses original interaction time");
clock.Now = initial.AddDays(10);
var retried = await Send(responseCookie, activity: respondedAt);
Check(Expiry(retried) == Expiry(response), "retrying an answer cannot extend inactivity deadline");
var older = await Send(Cookie(retried), activity: initial.AddDays(7));
Check(Expiry(older) == Expiry(response), "older queued activity cannot shorten deadline");
var future = await Send(Cookie(older), activity: clock.Now.AddDays(10));
Check(!future.Headers.Contains("Set-Cookie") && Expiry(future) == Expiry(response), "invalid future activity is ignored");

clock.Now = respondedAt.AddDays(7);
Check((await Send(Cookie(older), activity: clock.Now)).StatusCode == HttpStatusCode.Unauthorized,
    "session ends exactly after seven inactive days despite polling and retries");

clock.Now = initial;
var web = await Send(null, mobile: false, login: true);
Check(!web.Headers.Contains(MobileSessionPolicy.ExpirationHeader), "web login retains its existing policy");
clock.Now = initial.AddHours(9);
Check((await Send(Cookie(web), mobile: false)).StatusCode == HttpStatusCode.Unauthorized, "web expiration remains eight hours");
Check(MobileSessionPolicy.RenewedExpiration(initial, initial, initial.ToUnixTimeMilliseconds().ToString()) == null,
    "expiration helper rejects expired sessions");
Console.WriteLine($"{checks} backend session checks passed.");
await app.StopAsync();

sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
