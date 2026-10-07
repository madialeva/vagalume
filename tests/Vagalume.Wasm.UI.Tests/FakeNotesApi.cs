using Vagalume.Api.Contracts;

namespace Vagalume.Wasm.UI.Tests;

/// <summary>
/// In-memory <see cref="INotesApi"/> that behaves like the server, including its errors, so the page can be tested without a browser.
/// </summary>
internal sealed class FakeNotesApi : INotesApi
{
    private readonly List<NoteDto> _notes = [];
    private int _version = 1;

    public bool Fail { get; set; }

    public Task<IReadOnlyList<NoteDto>> ListAsync(CancellationToken cancellationToken) =>
        Fail ? throw new ApiException("down") : Task.FromResult<IReadOnlyList<NoteDto>>([.. _notes]);

    public Task<NoteDto> CreateAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ApiValidationException("The text of a note cannot be empty.");
        }

        var note = new NoteDto(Guid.NewGuid(), text.Trim(), DateTimeOffset.UtcNow, (_version++).ToString(System.Globalization.CultureInfo.InvariantCulture));
        _notes.Add(note);
        return Task.FromResult(note);
    }

    public Task<NoteDto> UpdateAsync(Guid id, string text, string version, CancellationToken cancellationToken)
    {
        var index = _notes.FindIndex(n => n.Id == id);
        if (index < 0)
        {
            throw new ApiNotFoundException("gone");
        }

        if (_notes[index].Version != version)
        {
            throw new ApiConflictException("stale");
        }

        _notes[index] = _notes[index] with { Text = text, Version = (_version++).ToString(System.Globalization.CultureInfo.InvariantCulture) };
        return Task.FromResult(_notes[index]);
    }

    public void ChangeBehindTheBack(Guid id, string text)
    {
        var index = _notes.FindIndex(n => n.Id == id);
        _notes[index] = _notes[index] with { Text = text, Version = (_version++).ToString(System.Globalization.CultureInfo.InvariantCulture) };
    }
}
