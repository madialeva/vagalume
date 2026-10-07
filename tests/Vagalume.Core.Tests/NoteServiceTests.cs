using Vagalume.Core;

namespace Vagalume.Core.Tests;

public sealed class NoteServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly InMemoryNoteRepository _repository = new();
    private readonly FixedClock _clock = new();
    private readonly NoteService _service;

    public NoteServiceTests()
    {
        _service = new NoteService(_repository, _clock);
    }

    [Fact]
    public async Task Create_TrimsTextAndStampsCreationTime()
    {
        var note = await _service.CreateAsync("  Hola  ", Ct);

        Assert.Equal("Hola", note.Text);
        Assert.Equal(_clock.UtcNow, note.CreatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_WithBlankText_IsRejectedAndNothingIsSaved(string text)
    {
        await Assert.ThrowsAsync<NoteValidationException>(() => _service.CreateAsync(text, Ct));

        Assert.Empty(await _service.ListAsync(Ct));
    }

    [Fact]
    public async Task List_ReturnsNotesInCreationOrder()
    {
        await _service.CreateAsync("segunda", Ct);
        _clock.UtcNow = _clock.UtcNow.AddMinutes(-5);
        await _service.CreateAsync("primera", Ct);

        var notes = await _service.ListAsync(Ct);

        Assert.Equal(["primera", "segunda"], notes.Select(n => n.Text));
    }

    [Fact]
    public async Task Update_WithCurrentVersion_ChangesTheText()
    {
        var note = await _service.CreateAsync("antes", Ct);

        var updated = await _service.UpdateTextAsync(note.Id, "después", _repository.VersionOf(note.Id), Ct);

        Assert.Equal("después", updated.Text);
    }

    [Fact]
    public async Task Update_WithStaleVersion_ConflictsAndKeepsTheFirstChange()
    {
        var note = await _service.CreateAsync("original", Ct);
        var read = _repository.VersionOf(note.Id);
        await _service.UpdateTextAsync(note.Id, "primero", read, Ct);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => _service.UpdateTextAsync(note.Id, "segundo", read, Ct));

        Assert.Equal("primero", (await _service.ListAsync(Ct)).Single().Text);
    }

    [Fact]
    public async Task Update_WithBlankText_IsRejected()
    {
        var note = await _service.CreateAsync("texto", Ct);

        await Assert.ThrowsAsync<NoteValidationException>(
            () => _service.UpdateTextAsync(note.Id, " ", _repository.VersionOf(note.Id), Ct));
    }

    [Fact]
    public async Task Update_OfMissingNote_Fails()
    {
        await Assert.ThrowsAsync<NoteNotFoundException>(() => _service.UpdateTextAsync(Guid.NewGuid(), "x", 1, Ct));
    }
}
