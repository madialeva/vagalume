using Vagalume.Core;

namespace Vagalume.Core.Tests;

/// <summary>
/// <see cref="IClock"/> that returns a time the test controls.
/// </summary>
internal sealed class FixedClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
}
