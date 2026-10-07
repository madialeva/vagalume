namespace Vagalume.Api.Contracts;

/// <summary>
/// Base of the errors a client sees when the API answers with an error status.
/// </summary>
public class ApiException : Exception
{
    public ApiException(string message)
        : base(message)
    {
    }
}
