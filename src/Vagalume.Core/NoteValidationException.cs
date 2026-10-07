namespace Vagalume.Core;

/// <summary>
/// Raised when the text of a note breaks a business rule.
/// </summary>
public sealed class NoteValidationException : Exception
{
    public NoteValidationException(string message)
        : base(message)
    {
    }
}
