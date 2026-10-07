namespace Vagalume.Core;

/// <summary>
/// Raised when a note does not exist.
/// </summary>
public sealed class NoteNotFoundException : Exception
{
    public NoteNotFoundException(Guid noteId)
        : base($"Note {noteId} does not exist.")
    {
    }
}
