namespace Vagalume.Api.Contracts;

/// <summary>
/// Body of the request that changes the text of a note; <see cref="Version"/> is the token read with the note.
/// </summary>
public sealed record UpdateNoteRequest(string Text, string Version);
