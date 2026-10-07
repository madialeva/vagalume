using System.Globalization;
using Vagalume.Api.Contracts;
using Vagalume.Wasm.UI.Markup;
using static Vagalume.Wasm.UI.Markup.Html;

namespace Vagalume.Wasm.UI.Notes;

/// <summary>
/// The notes page as a pure function of <see cref="NotesModel"/>: the same model always produces the same tree.
/// </summary>
public static class NotesView
{
    public static Node Render(NotesModel model) =>
        Div(
            Header(Class("app-header"), "Vagalume"),
            MainContent(
                Class("app-main"),
                H1("Notas"),
                Form(
                    Class("new-note"),
                    OnSubmit(model.CreateAsync),
                    Input(AriaLabel("Texto de la nota"), Placeholder("Escribe una nota"), BindValue(model.NewText, v => model.NewText = v)),
                    Button(Type("submit"), "Añadir")),
                When(model.Message is not null, () => P(Class("message"), Role("alert"), model.Message!)),
                List(model)));

    private static ElementNode List(NotesModel model) => model.Notes switch
    {
        null => P("Cargando…"),
        { Count: 0 } => P("Todavía no hay notas."),
        var notes => Ul(Class("notes"), Many(notes.Select(note => Row(model, note)))),
    };

    private static ElementNode Row(NotesModel model, NoteDto note) =>
        model.Editing == note.Id
            ? Li(
                Input(AriaLabel("Editar nota"), BindValue(model.EditText, v => model.EditText = v)),
                Button(Type("button"), OnClick(() => model.SaveAsync(note)), "Guardar"),
                Button(Type("button"), OnClick(model.CancelEdit), "Cancelar"))
            : Li(
                Span(Class("text"), note.Text),
                Time(DateTime(note.CreatedAt.ToString("O", CultureInfo.InvariantCulture)), note.CreatedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)),
                Button(Type("button"), OnClick(() => model.StartEdit(note)), "Editar"));
}
