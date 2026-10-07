namespace Vagalume.Api.Contracts;

/// <summary>
/// The note was changed by someone else after it was read (HTTP 409).
/// </summary>
public sealed class ApiConflictException : ApiException
{
    public ApiConflictException(string message)
        : base(message)
    {
    }
}
