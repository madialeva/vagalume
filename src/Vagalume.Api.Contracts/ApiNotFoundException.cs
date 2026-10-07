namespace Vagalume.Api.Contracts;

/// <summary>
/// The requested note does not exist (HTTP 404).
/// </summary>
public sealed class ApiNotFoundException : ApiException
{
    public ApiNotFoundException(string message)
        : base(message)
    {
    }
}
