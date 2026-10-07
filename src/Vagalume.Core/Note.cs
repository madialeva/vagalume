namespace Vagalume.Core;

/// <summary>
/// A short text note; the sample aggregate that exercises the whole stack.
/// </summary>
public sealed class Note
{
    public Note(Guid id, string text, DateTimeOffset createdAt)
    {
        Id = id;
        Text = text;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public string Text { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    /// <summary>Opaque optimistic-concurrency token assigned by the database.</summary>
    public uint Version { get; private set; }

    public void Rename(string text)
    {
        Text = text;
    }
}
