using Vagalume.Wasm.UI.Notes;

namespace Vagalume.Wasm.UI.Tests;

public sealed class NotesPageTests
{
    private readonly FakeNotesApi _api = new();
    private readonly NotesModel _model;

    public NotesPageTests()
    {
        _model = new NotesModel(_api);
    }

    private string Html => HtmlWriter.Render(NotesView.Render(_model));

    [Fact]
    public void BeforeLoading_ShowsALoadingMessage()
    {
        Assert.Contains("Cargando", Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AfterLoadingNothing_ShowsTheEmptyState()
    {
        await _model.LoadAsync();

        Assert.Contains("Todavía no hay notas.", Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreatingANote_ShowsItInTheListAndClearsTheInput()
    {
        await _model.LoadAsync();
        _model.NewText = "Hola";

        await _model.CreateAsync();

        Assert.Contains("<span class=\"text\">Hola</span>", Html, StringComparison.Ordinal);
        Assert.Equal(string.Empty, _model.NewText);
        Assert.DoesNotContain("role=\"alert\"", Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BlankText_ShowsTheServerValidationMessageAndAddsNothing()
    {
        await _model.LoadAsync();
        _model.NewText = "  ";

        await _model.CreateAsync();

        Assert.Contains("role=\"alert\"", Html, StringComparison.Ordinal);
        Assert.Contains("The text of a note cannot be empty.", Html, StringComparison.Ordinal);
        Assert.Contains("Todavía no hay notas.", Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EditingANote_ShowsAnInputWithItsTextAndSavesIt()
    {
        _model.NewText = "antes";
        await _model.CreateAsync();
        var note = _model.Notes!.Single();

        _model.StartEdit(note);
        Assert.Contains("value=\"antes\"", Html, StringComparison.Ordinal);
        _model.EditText = "después";
        await _model.SaveAsync(note);

        Assert.Contains("<span class=\"text\">después</span>", Html, StringComparison.Ordinal);
        Assert.Null(_model.Editing);
    }

    [Fact]
    public async Task Conflict_TellsTheUserReloadsTheListAndKeepsTheOtherPersonsChange()
    {
        _model.NewText = "original";
        await _model.CreateAsync();
        var note = _model.Notes!.Single();
        _model.StartEdit(note);
        _api.ChangeBehindTheBack(note.Id, "de otra persona");
        _model.EditText = "mi cambio";

        await _model.SaveAsync(note);

        Assert.Contains("Otra persona ha modificado esta nota", Html, StringComparison.Ordinal);
        Assert.Contains("de otra persona", Html, StringComparison.Ordinal);
        Assert.DoesNotContain("mi cambio", Html, StringComparison.Ordinal);
        Assert.Null(_model.Editing);
    }

    [Fact]
    public async Task ServerDown_ShowsAMessageInsteadOfFailing()
    {
        _api.Fail = true;

        await _model.LoadAsync();

        Assert.Contains("No se pudo cargar la lista de notas.", Html, StringComparison.Ordinal);
    }
}
