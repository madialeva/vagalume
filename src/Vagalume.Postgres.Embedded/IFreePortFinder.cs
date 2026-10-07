namespace Vagalume.Postgres.Embedded;

/// <summary>
/// Collaborator that finds a TCP port on the loopback interface that nobody is using.
/// </summary>
public interface IFreePortFinder
{
    int FindFreePort();
}
