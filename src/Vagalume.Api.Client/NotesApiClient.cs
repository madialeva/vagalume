using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Vagalume.Api.Contracts;

namespace Vagalume.Api.Client;

/// <summary>
/// <see cref="INotesApi"/> over HTTP: calls the REST API and turns error statuses into the exceptions of the contract.
/// </summary>
public sealed class NotesApiClient : INotesApi
{
    private readonly HttpClient _http;

    public NotesApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<NoteDto>> ListAsync(CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(ApiRoutes.Notes, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<NoteDto>>(cancellationToken) ?? [];
    }

    public async Task<NoteDto> CreateAsync(string text, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync(ApiRoutes.Notes, new CreateNoteRequest(text), cancellationToken);
        return await ReadNoteAsync(response, cancellationToken);
    }

    public async Task<NoteDto> UpdateAsync(Guid id, string text, string version, CancellationToken cancellationToken)
    {
        using var response = await _http.PutAsJsonAsync(ApiRoutes.Note(id), new UpdateNoteRequest(text, version), cancellationToken);
        return await ReadNoteAsync(response, cancellationToken);
    }

    private static async Task<NoteDto> ReadNoteAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<NoteDto>(cancellationToken)
            ?? throw new ApiException("The server returned an empty answer.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var title = await ReadTitleAsync(response, cancellationToken);
        throw response.StatusCode switch
        {
            HttpStatusCode.BadRequest => new ApiValidationException(title ?? "The request is not valid."),
            HttpStatusCode.NotFound => new ApiNotFoundException(title ?? "The note does not exist."),
            HttpStatusCode.Conflict => new ApiConflictException(title ?? "The note was modified by someone else."),
            _ => new ApiException("The server could not complete the request."),
        };
    }

    private static async Task<string?> ReadTitleAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            return document.RootElement.TryGetProperty("title", out var title) ? title.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
