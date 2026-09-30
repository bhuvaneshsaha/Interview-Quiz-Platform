using InterviewQuiz.Kernel.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace InterviewQuiz.Host.Hosting;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            EntityNotFoundException => (StatusCodes.Status404NotFound, "Not Found", exception.Message),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden", exception.Message),
            ConcurrencyException => (StatusCodes.Status409Conflict, "Conflict", exception.Message),
            DomainException => (StatusCodes.Status400BadRequest, "Bad Request", exception.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Conflict",
                "The resource was modified by another request. Reload and retry."),
            _ => (StatusCodes.Status500InternalServerError, "Server Error", "An unexpected error occurred.")
        };

        if (status >= 500)
        {
            _logger.LogError(exception, "Unhandled exception. CorrelationId {CorrelationId}", httpContext.TraceIdentifier);
        }
        else
        {
            _logger.LogWarning(exception, "Request failed with {StatusCode}. CorrelationId {CorrelationId}",
                status, httpContext.TraceIdentifier);
        }

        httpContext.Response.StatusCode = status;
        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = status,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path
            }
        });
    }
}
