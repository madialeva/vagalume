namespace Vagalume.Api.Contracts;

/// <summary>
/// The API rejected the input (HTTP 400); the message is meant to be shown to the user.
/// </summary>
public sealed class ApiValidationException : ApiException
{
    public ApiValidationException(string message)
        : base(message)
    {
    }
}
