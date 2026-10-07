namespace Vagalume.Postgres.Embedded;

/// <summary>
/// Outcome of a finished child process: exit code and the text it wrote.
/// </summary>
public sealed record ProcessResult(int ExitCode, string Output, string Error)
{
    public void EnsureSuccess(string description)
    {
        if (ExitCode != 0)
        {
            throw new EmbeddedPostgresException(
                $"{description} failed with exit code {ExitCode}.{Environment.NewLine}{Output}{Error}".TrimEnd());
        }
    }
}
