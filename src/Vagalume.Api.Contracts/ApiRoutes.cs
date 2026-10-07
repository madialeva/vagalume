namespace Vagalume.Api.Contracts;

/// <summary>
/// The paths of the REST API, shared so server and client cannot drift apart.
/// </summary>
public static class ApiRoutes
{
    public const string Notes = "/api/notes";

    public static string Note(Guid id) => $"{Notes}/{id}";
}
