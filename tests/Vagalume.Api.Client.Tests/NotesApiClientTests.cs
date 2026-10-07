using Vagalume.Api.Client;
using Vagalume.Api.Contracts;
using Vagalume.Core;
using Vagalume.Testing;

namespace Vagalume.Api.Client.Tests;

[Trait("Category", "Integration")]
public sealed class NotesApiClientTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task CreateListAndUpdate_WorkOverRealHttp()
    {
        await using var cluster = await StartedClusterAsync();
        await using var server = await ApiTestServer.StartAsync(cluster.Host.ConnectionString);
        using var http = server.NewClient();
        var api = new NotesApiClient(http);

        var created = await api.CreateAsync("uno", Ct);
        var updated = await api.UpdateAsync(created.Id, "dos", created.Version, Ct);
        var list = await api.ListAsync(Ct);

        Assert.Equal("uno", created.Text);
        Assert.Equal("dos", updated.Text);
        Assert.Equal("dos", Assert.Single(list).Text);
    }

    [Fact]
    public async Task BlankText_IsTranslatedToAValidationException()
    {
        await using var cluster = await StartedClusterAsync();
        await using var server = await ApiTestServer.StartAsync(cluster.Host.ConnectionString);
        using var http = server.NewClient();
        var api = new NotesApiClient(http);

        var error = await Assert.ThrowsAsync<ApiValidationException>(() => api.CreateAsync("  ", Ct));

        Assert.Contains("empty", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingNote_IsTranslatedToANotFoundException()
    {
        await using var cluster = await StartedClusterAsync();
        await using var server = await ApiTestServer.StartAsync(cluster.Host.ConnectionString);
        using var http = server.NewClient();
        var api = new NotesApiClient(http);

        await Assert.ThrowsAsync<ApiNotFoundException>(() => api.UpdateAsync(Guid.NewGuid(), "x", "1", Ct));
    }

    [Fact]
    public async Task StaleVersion_IsTranslatedToAConflictException()
    {
        await using var cluster = await StartedClusterAsync();
        await using var server = await ApiTestServer.StartAsync(cluster.Host.ConnectionString);
        using var http = server.NewClient();
        var api = new NotesApiClient(http);
        var note = await api.CreateAsync("original", Ct);
        await api.UpdateAsync(note.Id, "primero", note.Version, Ct);

        await Assert.ThrowsAsync<ApiConflictException>(() => api.UpdateAsync(note.Id, "segundo", note.Version, Ct));

        Assert.Equal("primero", Assert.Single(await api.ListAsync(Ct)).Text);
    }

    [Fact]
    public async Task ServerFailure_IsAGenericApiExceptionWithoutDetails()
    {
        await using var cluster = await StartedClusterAsync();
        await using var server = await ApiTestServer.StartAsync(cluster.Host.ConnectionString, new FailingRepository());
        using var http = server.NewClient();
        var api = new NotesApiClient(http);

        var error = await Assert.ThrowsAsync<ApiException>(() => api.ListAsync(Ct));

        Assert.Equal(typeof(ApiException), error.GetType());
        Assert.DoesNotContain("secret", error.Message, StringComparison.Ordinal);
    }

    private static async Task<TestCluster> StartedClusterAsync()
    {
        var cluster = new TestCluster();
        await cluster.Host.StartAsync(Ct);
        return cluster;
    }

    private sealed class FailingRepository : INoteRepository
    {
        public Task AddAsync(Note note, CancellationToken cancellationToken) => throw new InvalidOperationException("secret");

        public Task<IReadOnlyList<Note>> ListAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("secret");

        public Task<Note> UpdateTextAsync(Guid id, string text, uint expectedVersion, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("secret");
    }
}
