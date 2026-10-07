namespace Vagalume.Api.Contracts;

/// <summary>
/// What a client can do with notes; implemented over HTTP by the API client and replaceable in tests.
/// </summary>
public interface INotesApi
{
    Task<IReadOnlyList<NoteDto>> ListAsync(CancellationToken cancellationToken);

    /// <exception cref="ApiValidationException">The text is not valid.</exception>
    Task<NoteDto> CreateAsync(string text, CancellationToken cancellationToken);

    /// <exception cref="ApiValidationException">The text is not valid.</exception>
    /// <exception cref="ApiNotFoundException">The note does not exist.</exception>
    /// <exception cref="ApiConflictException">The note changed since <paramref name="version"/> was read.</exception>
    Task<NoteDto> UpdateAsync(Guid id, string text, string version, CancellationToken cancellationToken);
}
