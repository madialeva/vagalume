using Microsoft.AspNetCore.Components;
using Vagalume.Api.Contracts;
using Vagalume.Wasm.UI.Markup;

namespace Vagalume.Wasm.UI.Notes;

/// <summary>
/// The component that mounts the notes page: it owns the <see cref="NotesModel"/> and delegates the view to <see cref="NotesView"/>.
/// </summary>
public sealed class NotesPage : MarkupComponent
{
    private NotesModel? _model;

    [Inject]
    public INotesApi Api { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        _model = new NotesModel(Api);
        await _model.LoadAsync();
    }

    protected override Node Render() => _model is null ? Html.Empty : NotesView.Render(_model);
}
