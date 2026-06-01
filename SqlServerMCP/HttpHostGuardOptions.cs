using System.Net;
using Microsoft.AspNetCore.Http;
using System.Linq;

namespace SqlServerMCP;

public sealed class HttpHostGuardOptions
{
    public bool Enabled { get; init; }

    public IReadOnlyList<string> AllowedHosts { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AllowedOrigins { get; init; } = Array.Empty<string>();

    public static HttpHostGuardOptions FromEnvironment()
    {
        var enabled = ParseBool(Environment.GetEnvironmentVariable("MCP_HTTP_HOST_GUARD_ENABLED"), false);
        var hosts = Environment.GetEnvironmentVariable("MCP_ALLOWED_HOSTS");
        var origins = Environment.GetEnvironmentVariable("MCP_ALLOWED_ORIGINS");

        var hostList = string.IsNullOrWhiteSpace(hosts)
            ? Array.Empty<string>()
            : hosts.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var originList = string.IsNullOrWhiteSpace(origins)
            ? Array.Empty<string>()
            : origins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new HttpHostGuardOptions
        {
            Enabled = enabled,
            AllowedHosts = hostList,
            AllowedOrigins = originList
        };
    }

    public void Validate()
    {
        if (!Enabled)
            return;

        // When enabled, at least one of hosts or origins should be configured.
        if ((AllowedHosts == null || AllowedHosts.Count == 0) && (AllowedOrigins == null || AllowedOrigins.Count == 0))
            throw new InvalidOperationException("MCP_HTTP_HOST_GUARD_ENABLED=true requires MCP_ALLOWED_HOSTS and/or MCP_ALLOWED_ORIGINS to be set.");
    }

    public bool IsHostAllowed(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        if (AllowedHosts == null || AllowedHosts.Count == 0)
            return true; // no restriction

        // Normalize
        var hostLower = host.Trim().ToLowerInvariant();

        foreach (var pattern in AllowedHosts)
        {
            var p = pattern.Trim().ToLowerInvariant();
            if (p == "*")
                return true;

            if (p.StartsWith("*."))
            {
                var suffix = p[2..];
                if (hostLower == suffix || hostLower.EndsWith('.' + suffix))
                    return true;
            }
            else
            {
                if (hostLower == p)
                    return true;
            }
        }

        return false;
    }

    public bool IsOriginAllowed(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
            return false;

        if (AllowedOrigins == null || AllowedOrigins.Count == 0)
            return true; // no restriction

        // Try parse origin as URI
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            return false;

        var hostLower = uri.Host.Trim().ToLowerInvariant();

        foreach (var pattern in AllowedOrigins)
        {
            var p = pattern.Trim().ToLowerInvariant();
            if (p == "*")
                return true;

            // allow patterns that include scheme (e.g., https://example.com)
            if (p.Contains("://"))
            {
                if (string.Equals(p, origin.Trim(), StringComparison.OrdinalIgnoreCase))
                    return true;
                // also compare host-only
                try
                {
                    if (Uri.TryCreate(p, UriKind.Absolute, out var pu) && pu.Host == uri.Host && pu.Scheme == uri.Scheme)
                        return true;
                }
                catch { }
            }
            else if (p.StartsWith("*."))
            {
                var suffix = p[2..];
                if (hostLower == suffix || hostLower.EndsWith('.' + suffix))
                    return true;
            }
            else
            {
                if (hostLower == p)
                    return true;
            }
        }

        return false;
    }

    private static bool ParseBool(string? value, bool defaultValue)
        => bool.TryParse(value, out var parsed) ? parsed : defaultValue;
}

public sealed class HttpHostGuardMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));

    public async Task InvokeAsync(HttpContext context, HttpHostGuardOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));

        var host = context.Request.Host.Host;
        if (!options.IsHostAllowed(host))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = true, errorCode = "MCP-HOST-001", message = "Host no permitido." });
            return;
        }

        if (context.Request.Headers.TryGetValue("Origin", out var originValues))
        {
            var origin = originValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(origin) && !options.IsOriginAllowed(origin))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = true, errorCode = "MCP-HOST-002", message = "Origin no permitido." });
                return;
            }
        }

        await _next(context);
    }
}
