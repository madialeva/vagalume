using Microsoft.AspNetCore.Http;
using Vagalume.Core;

namespace Vagalume.Api;

/// <summary>
/// Endpoint filter that turns domain errors into standard HTTP problem responses and hides every other failure
/// behind a generic 500 so no stack trace, type name or database detail reaches the client.
/// </summary>
public sealed class ErrorMappingFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (NoteValidationException error)
        {
            return TypedResults.Problem(title: error.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (NoteNotFoundException error)
        {
            return TypedResults.Problem(title: error.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (ConcurrencyConflictException error)
        {
            return TypedResults.Problem(title: error.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return TypedResults.Problem(title: "Unexpected error.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
