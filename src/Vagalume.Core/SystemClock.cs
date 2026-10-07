namespace Vagalume.Core;

/// <summary>
/// <see cref="IClock"/> backed by the system clock.
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
