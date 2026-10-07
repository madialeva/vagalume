namespace Vagalume.Core;

/// <summary>
/// Collaborator that tells the current time, so use cases do not read the system clock directly.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
