// Middleware/RequestLoggingMiddleware.cs
using System.Diagnostics;

namespace UserManagementAPI.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        var method = context.Request.Method;
        var path = context.Request.Path;

        _logger.LogInformation("Incoming request: {Method} {Path}", method, path);

        try
        {
            await _next(context);
        }
        catch
        {
            // The error middleware (outer) will turn this into a 500.
            _logger.LogInformation("Outgoing response: {Method} {Path} -> {StatusCode} ({Elapsed} ms)",
                method, path, StatusCodes.Status500InternalServerError, sw.ElapsedMilliseconds);
            throw;
        }

        _logger.LogInformation("Outgoing response: {Method} {Path} -> {StatusCode} ({Elapsed} ms)",
            method, path, context.Response.StatusCode, sw.ElapsedMilliseconds);
    }
}