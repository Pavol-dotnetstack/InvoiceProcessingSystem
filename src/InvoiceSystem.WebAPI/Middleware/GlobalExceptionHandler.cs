using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceSystem.WebAPI.Middleware;

public class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger, 
    IWebHostEnvironment env) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);

        // Standardized RFC 7807 Problem Details
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Server Error",
            Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1",
            Detail = env.IsDevelopment() ? exception.Message : "An internal error occurred. Please try again later."
        };

        httpContext.Response.StatusCode = problemDetails.Status.Value;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true; // Indicates the exception has been handled
    }
}