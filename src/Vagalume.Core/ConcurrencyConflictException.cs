namespace Vagalume.Core;

/// <summary>
/// Raised when a note was changed by someone else after it was read.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(Guid noteId)
        : base($"Note {noteId} was modified by someone else since it was read.")
    {
    }
}
