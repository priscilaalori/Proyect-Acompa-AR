using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

// Android renews only for explicit user activity. Web sessions retain their existing policy.
internal static class MobileSessionPolicy
{
    internal static readonly TimeSpan Inactivity = TimeSpan.FromDays(7);
    internal const string ExpirationHeader = "X-Acompaar-Session-Expires";

    internal static bool IsMobile(HttpContext context) =>
        context.Request.Headers["X-Acompaar-Client"] == "android";

    internal static DateTimeOffset UtcNow(HttpContext context) =>
        (context.RequestServices.GetService<TimeProvider>() ?? TimeProvider.System).GetUtcNow();

    internal static AuthenticationProperties? LoginProperties(HttpContext context)
    {
        if (!IsMobile(context)) return null;
        var now = UtcNow(context);
        return Properties(now, now.Add(Inactivity));
    }

    internal static AuthenticationProperties Properties(DateTimeOffset now, DateTimeOffset expires) => new()
    {
        IssuedUtc = now,
        ExpiresUtc = expires,
        IsPersistent = true,
        // Background polling must never renew an Android session automatically.
        AllowRefresh = false
    };

    internal static DateTimeOffset? RenewedExpiration(DateTimeOffset now, DateTimeOffset expires, string activity)
    {
        // This method cannot revive an expired ticket, even with a fresh activity timestamp.
        if (expires <= now || !long.TryParse(activity, out var milliseconds)) return null;
        if (milliseconds < now.Subtract(Inactivity).ToUnixTimeMilliseconds()
            || milliseconds > now.AddMinutes(5).ToUnixTimeMilliseconds()) return null;
        var occurred = DateTimeOffset.FromUnixTimeMilliseconds(Math.Min(milliseconds, now.ToUnixTimeMilliseconds()));
        var renewed = occurred.Add(Inactivity);
        // An older queued response must not shorten a session renewed by a newer interaction.
        return renewed > expires ? renewed : expires;
    }

    internal static void WriteExpiration(HttpContext context, AuthenticationProperties? properties)
    {
        if (IsMobile(context) && properties?.ExpiresUtc is { } expires)
            context.Response.Headers[ExpirationHeader] = expires.ToUnixTimeMilliseconds().ToString();
    }
}

internal sealed class MobileSessionMiddleware
{
    private readonly RequestDelegate next;
    public MobileSessionMiddleware(RequestDelegate next) => this.next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (MobileSessionPolicy.IsMobile(context) && context.User.Identity?.IsAuthenticated == true
            && context.Request.Path != "/api/auth/login")
        {
            var ticket = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var properties = ticket.Properties;
            if (ticket.Succeeded && properties?.ExpiresUtc is { } expires)
            {
                var now = MobileSessionPolicy.UtcNow(context);
                if (expires <= now)
                {
                    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new { mensaje = "La sesión venció. Ingresá nuevamente." });
                    return;
                }
                var renewed = MobileSessionPolicy.RenewedExpiration(now, expires,
                    context.Request.Headers["X-Acompaar-Activity"].ToString());
                if (renewed is { } renewedExpiry && context.User.IsInRole("MAYOR"))
                {
                    // Also upgrades still-valid Android cookies issued by the previous eight-hour policy.
                    properties = MobileSessionPolicy.Properties(now, renewedExpiry);
                    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, context.User, properties);
                }
                MobileSessionPolicy.WriteExpiration(context, properties);
            }
        }
        await next(context);
    }
}
