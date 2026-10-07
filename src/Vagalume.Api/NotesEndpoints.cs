using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vagalume.Api.Contracts;
using Vagalume.Core;

namespace Vagalume.Api;

/// <summary>
/// Maps the REST API of notes over the <see cref="NoteService"/> use cases.
/// </summary>
public static class NotesEndpoints
{
    public static IEndpointRouteBuilder MapNotesApi(this IEndpointRouteBuilder endpoints)
    {
        var notes = endpoints.MapGroup(ApiRoutes.Notes).AddEndpointFilter<ErrorMappingFilter>();

        notes.MapGet(
            "/",
            async (NoteService service, CancellationToken cancellationToken) =>
                TypedResults.Ok((await service.ListAsync(cancellationToken)).Select(ToDto).ToList()));

        notes.MapPost(
            "/",
            async (CreateNoteRequest request, NoteService service, CancellationToken cancellationToken) =>
            {
                var note = await service.CreateAsync(request.Text, cancellationToken);
                return TypedResults.Created(ApiRoutes.Note(note.Id), ToDto(note));
            });

        notes.MapPut(
            "/{id:guid}",
            async (Guid id, UpdateNoteRequest request, NoteService service, CancellationToken cancellationToken) =>
            {
                if (!uint.TryParse(request.Version, NumberStyles.None, CultureInfo.InvariantCulture, out var version))
                {
                    throw new NoteValidationException("The version of the note is not valid.");
                }

                return TypedResults.Ok(ToDto(await service.UpdateTextAsync(id, request.Text, version, cancellationToken)));
            });

        return endpoints;
    }

    private static NoteDto ToDto(Note note) =>
        new(note.Id, note.Text, note.CreatedAt, note.Version.ToString(CultureInfo.InvariantCulture));
}
