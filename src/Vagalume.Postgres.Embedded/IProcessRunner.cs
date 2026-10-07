namespace Vagalume.Postgres.Embedded;

/// <summary>
/// Collaborator that runs an executable with an explicit argument list and captures its result.
/// </summary>
public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}
