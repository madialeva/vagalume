using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vagalume.Core;
using Vagalume.Data;
using Vagalume.Testing;

namespace Vagalume.Data.Tests;

[Trait("Category", "Integration")]
public sealed class NoteDataTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task EmptyDatabase_IsMigratedAndStoresNotes()
    {
        await using var cluster = await StartedClusterAsync();

        var factory = FactoryOf(cluster);
        await new DatabaseMigrator(factory).MigrateAsync(Ct);
        var service = new NoteService(new NoteRepository(factory), new SystemClock());
        await service.CreateAsync("uno", Ct);
        await service.CreateAsync("dos", Ct);

        var notes = await new NoteRepository(FactoryOf(cluster)).ListAsync(Ct);
        Assert.Equal(["uno", "dos"], notes.Select(n => n.Text));
        Assert.All(notes, n => Assert.NotEqual(0u, n.Version));
    }

    [Fact]
    public async Task RepeatedMigration_LeavesSchemaAndDataUntouched()
    {
        await using var cluster = await StartedClusterAsync();
        var factory = FactoryOf(cluster);
        await new DatabaseMigrator(factory).MigrateAsync(Ct);
        await new NoteRepository(factory).AddAsync(new Note(Guid.NewGuid(), "persistente", DateTimeOffset.UtcNow), Ct);

        await new DatabaseMigrator(factory).MigrateAsync(Ct);

        Assert.Single(await new NoteRepository(factory).ListAsync(Ct));
        await using var context = factory.CreateDbContext();
        Assert.Single(await context.Database.GetAppliedMigrationsAsync(Ct));
        Assert.Empty(await context.Database.GetPendingMigrationsAsync(Ct));
    }

    [Fact]
    public async Task Notes_SurviveServerRestart()
    {
        await using var cluster = await StartedClusterAsync();
        var factory = FactoryOf(cluster);
        await new DatabaseMigrator(factory).MigrateAsync(Ct);
        await new NoteRepository(factory).AddAsync(new Note(Guid.NewGuid(), "sobrevive", DateTimeOffset.UtcNow), Ct);

        NpgsqlConnection.ClearAllPools();
        await cluster.Host.StopAsync(Ct);
        var restarted = TestCluster.NewHost(cluster.DataDirectory);
        await restarted.StartAsync(Ct);

        var after = new VagalumeDbContextFactory(restarted.ConnectionString);
        Assert.Equal("sobrevive", (await new NoteRepository(after).ListAsync(Ct)).Single().Text);
    }

    [Fact]
    public async Task StaleUpdate_ConflictsAndKeepsFirstChange()
    {
        await using var cluster = await StartedClusterAsync();
        var repository = new NoteRepository(FactoryOf(cluster));
        await new DatabaseMigrator(FactoryOf(cluster)).MigrateAsync(Ct);
        var note = new Note(Guid.NewGuid(), "original", DateTimeOffset.UtcNow);
        await repository.AddAsync(note, Ct);
        var readVersion = (await repository.ListAsync(Ct)).Single().Version;

        await repository.UpdateTextAsync(note.Id, "primero", readVersion, Ct);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => repository.UpdateTextAsync(note.Id, "segundo", readVersion, Ct));

        Assert.Equal("primero", (await repository.ListAsync(Ct)).Single().Text);

        var current = (await repository.ListAsync(Ct)).Single().Version;
        Assert.Equal("tercero", (await repository.UpdateTextAsync(note.Id, "tercero", current, Ct)).Text);
    }

    [Fact]
    public async Task Update_OfMissingNote_Fails()
    {
        await using var cluster = await StartedClusterAsync();
        var factory = FactoryOf(cluster);
        await new DatabaseMigrator(factory).MigrateAsync(Ct);

        await Assert.ThrowsAsync<NoteNotFoundException>(
            () => new NoteRepository(factory).UpdateTextAsync(Guid.NewGuid(), "x", 1, Ct));
    }

    private static async Task<TestCluster> StartedClusterAsync()
    {
        var cluster = new TestCluster();
        await cluster.Host.StartAsync(Ct);
        return cluster;
    }

    private static VagalumeDbContextFactory FactoryOf(TestCluster cluster) => new(cluster.Host.ConnectionString);
}
