using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OurSpace.Profiles.Domain;

namespace OurSpace.Profiles.Api;

public sealed class ProfileExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ProfileValidationException && exception is not UnauthorizedAccessException)
        {
            return false;
        }

        httpContext.Response.StatusCode = exception is ProfileValidationException
            ? StatusCodes.Status400BadRequest : StatusCodes.Status401Unauthorized;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = httpContext.Response.StatusCode,
                Title = exception is ProfileValidationException ? "Invalid profile" : "Authentication required",
                Detail = exception.Message,
                Extensions = { ["correlationId"] = httpContext.TraceIdentifier },
            },
        });
    }
}
