using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OurSpace.Profiles.Application;
using OurSpace.Profiles.Domain;

namespace OurSpace.Profiles.Api;

public sealed class ProfileExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not (ProfileValidationException or UnauthorizedAccessException or ProfileAlreadyExistsException))
        {
            return false;
        }

        var (status, title) = exception switch
        {
            ProfileValidationException => (StatusCodes.Status400BadRequest, "Invalid profile"),
            ProfileAlreadyExistsException => (StatusCodes.Status409Conflict, "Profile already exists"),
            _ => (StatusCodes.Status401Unauthorized, "Authentication required"),
        };
        httpContext.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = httpContext.Response.StatusCode,
                Title = title,
                Detail = exception.Message,
                Extensions = { ["correlationId"] = httpContext.TraceIdentifier },
            },
        });
    }
}
