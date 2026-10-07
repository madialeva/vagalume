namespace Vagalume.Core;

/// <summary>
/// Persistence role the use cases need for notes; implemented by the Data layer.
/// </summary>
public interface INoteRepository
{
    Task AddAsync(Note note, CancellationToken cancellationToken);

    /// <summary>Returns all notes in creation order.</summary>
    Task<IReadOnlyList<Note>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Changes the text of a note if it still has <paramref name="expectedVersion"/>; otherwise throws <see cref="ConcurrencyConflictException"/>.</summary>
    Task<Note> UpdateTextAsync(Guid id, string text, uint expectedVersion, CancellationToken cancellationToken);
}
