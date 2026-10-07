namespace Vagalume.Api.Contracts;

/// <summary>
/// Body of the request that creates a note.
/// </summary>
public sealed record CreateNoteRequest(string Text);
