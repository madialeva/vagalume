namespace Vagalume.Api.Contracts;

/// <summary>
/// A note as the API shows it; <see cref="Version"/> is an opaque token the client sends back when it edits.
/// </summary>
public sealed record NoteDto(Guid Id, string Text, DateTimeOffset CreatedAt, string Version);
