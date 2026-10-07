using Vagalume.Core;

namespace Vagalume.Core.Tests;

/// <summary>
/// Test double of <see cref="INoteRepository"/> that mimics the version check of the database.
/// </summary>
internal sealed class InMemoryNoteRepository : INoteRepository
{
    private readonly List<(Note Note, uint Version)> _rows = [];

    public Task AddAsync(Note note, CancellationToken cancellationToken)
    {
        _rows.Add((note, 1));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Note>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Note>>([.. _rows.Select(r => r.Note).OrderBy(n => n.CreatedAt)]);

    public Task<Note> UpdateTextAsync(Guid id, string text, uint expectedVersion, CancellationToken cancellationToken)
    {
        var index = _rows.FindIndex(r => r.Note.Id == id);
        if (index < 0)
        {
            throw new NoteNotFoundException(id);
        }

        if (_rows[index].Version != expectedVersion)
        {
            throw new ConcurrencyConflictException(id);
        }

        _rows[index].Note.Rename(text);
        _rows[index] = (_rows[index].Note, expectedVersion + 1);
        return Task.FromResult(_rows[index].Note);
    }

    public uint VersionOf(Guid id) => _rows.Single(r => r.Note.Id == id).Version;
}
