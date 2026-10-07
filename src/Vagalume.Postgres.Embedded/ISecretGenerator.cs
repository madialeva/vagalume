namespace Vagalume.Postgres.Embedded;

/// <summary>
/// Collaborator that produces the random password of the embedded server.
/// </summary>
public interface ISecretGenerator
{
    string Generate();
}
