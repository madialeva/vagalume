using Microsoft.EntityFrameworkCore;
using Vagalume.Core;

namespace Vagalume.Data;

/// <summary>
/// EF Core model of the application; PostgreSQL is the only supported provider.
/// </summary>
public sealed class VagalumeDbContext : DbContext
{
    public VagalumeDbContext(DbContextOptions<VagalumeDbContext> options)
        : base(options)
    {
    }

    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Note>(note =>
        {
            note.ToTable("notes");
            note.HasKey(n => n.Id);
            note.Property(n => n.Text).IsRequired();
            note.Property(n => n.CreatedAt).IsRequired();
            note.Property(n => n.Version).IsRowVersion();
        });
    }
}
