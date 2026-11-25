// Middleware/TokenAuthenticationMiddleware.cs
using System.Security.Cryptography;
using System.Text;

namespace UserManagementAPI.Middleware;

public class TokenAuthenticationMiddleware
{
    private static readonly string[] AnonymousPrefixes = { "/openapi" };

    private readonly RequestDelegate _next;
    private readonly ILogger<TokenAuthenticationMiddleware> _logger;
    private readonly byte[][] _validTokens;

    public TokenAuthenticationMiddleware(
        RequestDelegate next,
        IConfiguration config,
        ILogger<TokenAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _validTokens = (config.GetSection("Auth:Tokens").Get<string[]>() ?? Array.Empty<string>())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(Encoding.UTF8.GetBytes)
            .ToArray();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (AnonymousPrefixes.Any(p => path.StartsWithSegments(p)))
        {
            await _next(context);
            return;
        }

        var token = ExtractBearerToken(context);

        if (token is null || !IsValid(token))
        {
            // Logged here because this request never reaches the logging middleware.
            _logger.LogWarning("Unauthorized: {Method} {Path} -> 401",
                context.Request.Method, context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized." });
            return;
        }

        await _next(context);
    }

    private static string? ExtractBearerToken(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        return header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? header[prefix.Length..].Trim()
            : null;
    }

    private bool IsValid(string token)
    {
        var candidate = Encoding.UTF8.GetBytes(token);
        // Constant-time comparison to avoid timing attacks.
        return _validTokens.Any(valid => CryptographicOperations.FixedTimeEquals(valid, candidate));
    }
}