using Vagalume.Api.Contracts;

namespace Vagalume.Wasm.UI.Notes;

/// <summary>
/// State and behavior behind the notes page: it talks to the server only through <see cref="INotesApi"/>
/// and turns the errors of the contract into messages for the user.
/// </summary>
public sealed class NotesModel
{
    private readonly INotesApi _api;

    public NotesModel(INotesApi api)
    {
        _api = api;
    }

    public IReadOnlyList<NoteDto>? Notes { get; private set; }

    public string NewText { get; set; } = string.Empty;

    public string EditText { get; set; } = string.Empty;

    public Guid? Editing { get; private set; }

    public string? Message { get; private set; }

    public Task LoadAsync() => RunAsync(() => Task.CompletedTask);

    public Task CreateAsync() =>
        RunAsync(async () =>
        {
            await _api.CreateAsync(NewText, CancellationToken.None);
            NewText = string.Empty;
        });

    public void StartEdit(NoteDto note)
    {
        Editing = note.Id;
        EditText = note.Text;
        Message = null;
    }

    public void CancelEdit() => Editing = null;

    public Task SaveAsync(NoteDto note) =>
        RunAsync(async () =>
        {
            await _api.UpdateAsync(note.Id, EditText, note.Version, CancellationToken.None);
            Editing = null;
        });

    private async Task RunAsync(Func<Task> action)
    {
        Message = null;
        try
        {
            await action();
        }
        catch (ApiValidationException error)
        {
            Message = $"No se pudo guardar: {error.Message}";
        }
        catch (ApiConflictException)
        {
            Message = "Otra persona ha modificado esta nota. Se ha recargado la lista; vuelve a intentarlo.";
            Editing = null;
        }
        catch (ApiNotFoundException)
        {
            Message = "La nota ya no existe.";
            Editing = null;
        }
        catch (ApiException)
        {
            Message = "El servidor no pudo completar la operación.";
        }

        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        try
        {
            Notes = await _api.ListAsync(CancellationToken.None);
        }
        catch (ApiException)
        {
            Message ??= "No se pudo cargar la lista de notas.";
        }
    }
}
