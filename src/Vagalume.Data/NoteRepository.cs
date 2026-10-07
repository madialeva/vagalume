using Microsoft.EntityFrameworkCore;
using Vagalume.Core;

namespace Vagalume.Data;

/// <summary>
/// <see cref="INoteRepository"/> implemented with EF Core; maps PostgreSQL's <c>xmin</c> conflicts to domain errors.
/// Each operation uses its own context.
/// </summary>
public sealed class NoteRepository : INoteRepository
{
    private readonly IDbContextFactory<VagalumeDbContext> _contexts;

    public NoteRepository(IDbContextFactory<VagalumeDbContext> contexts)
    {
        _contexts = contexts;
    }

    public async Task AddAsync(Note note, CancellationToken cancellationToken)
    {
        await using var context = await _contexts.CreateDbContextAsync(cancellationToken);
        context.Notes.Add(note);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Note>> ListAsync(CancellationToken cancellationToken)
    {
        await using var context = await _contexts.CreateDbContextAsync(cancellationToken);
        return await context.Notes.AsNoTracking().OrderBy(n => n.CreatedAt).ThenBy(n => n.Id).ToListAsync(cancellationToken);
    }

    public async Task<Note> UpdateTextAsync(Guid id, string text, uint expectedVersion, CancellationToken cancellationToken)
    {
        await using var context = await _contexts.CreateDbContextAsync(cancellationToken);
        var note = await context.Notes.SingleOrDefaultAsync(n => n.Id == id, cancellationToken)
            ?? throw new NoteNotFoundException(id);
        context.Entry(note).Property(n => n.Version).OriginalValue = expectedVersion;
        note.Rename(text);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException(id);
        }

        return note;
    }
}
