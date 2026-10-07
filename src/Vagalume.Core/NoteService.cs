namespace Vagalume.Core;

/// <summary>
/// Use cases for notes: validates input and delegates persistence to <see cref="INoteRepository"/>.
/// </summary>
public sealed class NoteService
{
    private readonly INoteRepository _notes;
    private readonly IClock _clock;

    public NoteService(INoteRepository notes, IClock clock)
    {
        _notes = notes;
        _clock = clock;
    }

    public async Task<Note> CreateAsync(string text, CancellationToken cancellationToken)
    {
        var note = new Note(Guid.NewGuid(), Validate(text), _clock.UtcNow);
        await _notes.AddAsync(note, cancellationToken);
        return note;
    }

    public Task<IReadOnlyList<Note>> ListAsync(CancellationToken cancellationToken) =>
        _notes.ListAsync(cancellationToken);

    public Task<Note> UpdateTextAsync(Guid id, string text, uint expectedVersion, CancellationToken cancellationToken) =>
        _notes.UpdateTextAsync(id, Validate(text), expectedVersion, cancellationToken);

    private static string Validate(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new NoteValidationException("The text of a note cannot be empty.");
        }

        return text.Trim();
    }
}
