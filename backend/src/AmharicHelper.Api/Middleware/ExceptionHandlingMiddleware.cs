using System.Net;
using System.Text.Json;

namespace AmharicHelper.Api.Middleware;

/// <summary>Converts unhandled exceptions into a consistent JSON error response.</summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (NotImplementedException ex)
        {
            logger.LogWarning(ex, "Not implemented feature called.");
            await WriteAsync(context, HttpStatusCode.NotImplemented, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception.");
            await WriteAsync(context, HttpStatusCode.InternalServerError, "An unexpected error occurred.");
        }
    }

    private static async Task WriteAsync(HttpContext context, HttpStatusCode code, string message)
    {
        context.Response.StatusCode = (int)code;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = message }));
    }
}
