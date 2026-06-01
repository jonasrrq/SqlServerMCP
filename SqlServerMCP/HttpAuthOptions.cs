using Microsoft.AspNetCore.Http;

namespace SqlServerMCP;

public sealed class HttpAuthOptions
{
    public const string DefaultHeaderName = "X-MCP-Auth";

    public bool Enabled { get; init; }

    public string HeaderName { get; init; } = DefaultHeaderName;

    public string? Token { get; init; }

    public static HttpAuthOptions FromEnvironment()
    {
        var enabled = ParseBool(Environment.GetEnvironmentVariable("MCP_HTTP_AUTH_ENABLED"), false);
        var headerName = Environment.GetEnvironmentVariable("MCP_AUTH_HEADER_NAME");

        return new HttpAuthOptions
        {
            Enabled = enabled,
            HeaderName = string.IsNullOrWhiteSpace(headerName) ? DefaultHeaderName : headerName.Trim(),
            Token = Environment.GetEnvironmentVariable("MCP_AUTH_TOKEN")
        };
    }

    public void Validate()
    {
        if (!Enabled)
            return;

        if (string.IsNullOrWhiteSpace(Token))
        {
            throw new InvalidOperationException("MCP_HTTP_AUTH_ENABLED=true requiere MCP_AUTH_TOKEN.");
        }
    }

    private static bool ParseBool(string? value, bool defaultValue)
        => bool.TryParse(value, out var parsed) ? parsed : defaultValue;
}

public static class HttpAuthEvaluator
{
    public static bool IsAuthorized(HttpAuthOptions options, IHeaderDictionary headers)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(headers);

        if (!options.Enabled)
            return true;

        if (string.IsNullOrWhiteSpace(options.Token))
            return false;

        if (TryGetBearerToken(headers, out var bearerToken) && TokenMatches(options.Token, bearerToken))
            return true;

        if (headers.TryGetValue(options.HeaderName, out var headerValues))
        {
            foreach (var value in headerValues)
            {
                if (TokenMatches(options.Token, value))
                    return true;
            }
        }

        return false;
    }

    private static bool TryGetBearerToken(IHeaderDictionary headers, out string? token)
    {
        token = null;

        if (!headers.TryGetValue("Authorization", out var authorizationValues))
            return false;

        foreach (var authorizationValue in authorizationValues)
        {
            if (string.IsNullOrWhiteSpace(authorizationValue))
                continue;

            const string bearerPrefix = "Bearer ";
            if (authorizationValue.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                token = authorizationValue[bearerPrefix.Length..].Trim();
                return !string.IsNullOrWhiteSpace(token);
            }
        }

        return false;
    }

    private static bool TokenMatches(string expectedToken, string? providedToken)
        => !string.IsNullOrWhiteSpace(providedToken)
            && string.Equals(expectedToken, providedToken.Trim(), StringComparison.Ordinal);
}

public sealed class HttpAuthMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));

    public async Task InvokeAsync(HttpContext context, HttpAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        if (!HttpAuthEvaluator.IsAuthorized(options, context.Request.Headers))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            context.Response.Headers.WWWAuthenticate = "Bearer";
            await context.Response.WriteAsJsonAsync(new
            {
                error = true,
                errorCode = "MCP-AUTH-001",
                message = "No autorizado. Configura el token correcto o desactiva la autenticación HTTP."
            });
            return;
        }

        await _next(context);
    }
}