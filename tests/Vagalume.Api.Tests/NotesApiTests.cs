using System.Net;
using System.Net.Http.Json;
using Vagalume.Api.Contracts;
using Vagalume.Core;
using Vagalume.Testing;

namespace Vagalume.Api.Tests;

[Trait("Category", "Integration")]
public sealed class NotesApiTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task CreateThenList_ReturnsTheNoteWithAnOpaqueVersion()
    {
        await using var cluster = await StartedClusterAsync();
        await using var server = await ApiTestServer.StartAsync(cluster.Host.ConnectionString);
        using var client = server.NewClient();

        var created = await client.PostAsJsonAsync(ApiRoutes.Notes, new CreateNoteRequest("Hola"), Ct);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var note = await created.Content.ReadFromJsonAsync<NoteDto>(Ct);
        Assert.NotNull(note);
        Assert.Equal("Hola", note.Text);
        Assert.False(string.IsNullOrEmpty(note.Version));
        Assert.Equal(ApiRoutes.Note(note.Id), created.Headers.Location?.OriginalString);
        var listed = await client.GetFromJsonAsync<List<NoteDto>>(ApiRoutes.Notes, Ct);
        var single = Assert.Single(listed!);
        Assert.Equal((note.Id, note.Text, note.Version), (single.Id, single.Text, single.Version));
    }

    [Fact]
    public async Task Update_WithTheVersionRead_ChangesTheTextAndTheVersion()
    {
        await using var cluster = await StartedClusterAsync();
        await using var server = await ApiTestServer.StartAsync(cluster.Host.ConnectionString);
        using var client = server.NewClient();
        var note = await CreateAsync(client, "antes");

        var response = await client.PutAsJsonAsync(ApiRoutes.Note(note.Id), new UpdateNoteRequest("después", note.Version), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<NoteDto>(Ct);
        Assert.Equal("después", updated!.Text);
        Assert.NotEqual(note.Version, updated.Version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_WithBlankText_Answers400AndSavesNothing(string text)
    {
        await using var cluster = await StartedClusterAsync();
        await using var server = await ApiTestServer.StartAsync(cluster.Host.ConnectionString);
        using var client = server.NewClient();

        var response = await client.PostAsJsonAsync(ApiRoutes.Notes, new CreateNoteRequest(text), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemBody>(Ct);
        Assert.Contains("empty", problem!.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Empty((await client.GetFromJsonAsync<List<NoteDto>>(ApiRoutes.Notes, Ct))!);
    }

    [Fact]
    public async Task Update_OfAMissingNote_Answers404()
    {
        await using var cluster = await StartedClusterAsync();
        await using var server = await ApiTestServer.StartAsync(cluster.Host.ConnectionString);
        using var client = server.NewClient();

        var response = await client.PutAsJsonAsync(ApiRoutes.Note(Guid.NewGuid()), new UpdateNoteRequest("x", "1"), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithAStaleVersion_Answers409AndKeepsTheFirstChange()
    {
        await using var cluster = await StartedClusterAsync();
        await using var server = await ApiTestServer.StartAsync(cluster.Host.ConnectionString);
        using var client = server.NewClient();
        var note = await CreateAsync(client, "original");
        (await client.PutAsJsonAsync(ApiRoutes.Note(note.Id), new UpdateNoteRequest("primero", note.Version), Ct)).EnsureSuccessStatusCode();

        var second = await client.PutAsJsonAsync(ApiRoutes.Note(note.Id), new UpdateNoteRequest("segundo", note.Version), Ct);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("primero", (await client.GetFromJsonAsync<List<NoteDto>>(ApiRoutes.Notes, Ct))!.Single().Text);
    }

    [Fact]
    public async Task Update_WithAMalformedVersion_Answers400()
    {
        await using var cluster = await StartedClusterAsync();
        await using var server = await ApiTestServer.StartAsync(cluster.Host.ConnectionString);
        using var client = server.NewClient();
        var note = await CreateAsync(client, "x");

        var response = await client.PutAsJsonAsync(ApiRoutes.Note(note.Id), new UpdateNoteRequest("y", "not-a-version"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ErrorResponses_NeverRevealInternals()
    {
        await using var cluster = await StartedClusterAsync();
        await using var server = await ApiTestServer.StartAsync(cluster.Host.ConnectionString);
        await using var broken = await ApiTestServer.StartAsync(
            cluster.Host.ConnectionString, new ThrowingRepository("password=hunter2 Npgsql.PostgresException at Vagalume.Data"));
        using var client = server.NewClient();
        using var brokenClient = broken.NewClient();
        var note = await CreateAsync(client, "x");

        var bodies = new List<string>
        {
            await (await client.PostAsJsonAsync(ApiRoutes.Notes, new CreateNoteRequest(""), Ct)).Content.ReadAsStringAsync(Ct),
            await (await client.PutAsJsonAsync(ApiRoutes.Note(Guid.NewGuid()), new UpdateNoteRequest("x", "1"), Ct)).Content.ReadAsStringAsync(Ct),
            await (await client.PutAsJsonAsync(ApiRoutes.Note(note.Id), new UpdateNoteRequest("a", "999999"), Ct)).Content.ReadAsStringAsync(Ct),
        };
        var failure = await brokenClient.GetAsync(ApiRoutes.Notes, Ct);
        bodies.Add(await failure.Content.ReadAsStringAsync(Ct));

        Assert.Equal(HttpStatusCode.InternalServerError, failure.StatusCode);
        Assert.All(bodies, body =>
        {
            Assert.DoesNotContain("hunter2", body, StringComparison.Ordinal);
            Assert.DoesNotContain("Npgsql", body, StringComparison.Ordinal);
            Assert.DoesNotContain("Exception", body, StringComparison.Ordinal);
            Assert.DoesNotContain(" at ", body, StringComparison.Ordinal);
            Assert.DoesNotContain("Vagalume.", body, StringComparison.Ordinal);
        });
    }

    private static async Task<TestCluster> StartedClusterAsync()
    {
        var cluster = new TestCluster();
        await cluster.Host.StartAsync(Ct);
        return cluster;
    }

    private static async Task<NoteDto> CreateAsync(HttpClient client, string text)
    {
        var response = await client.PostAsJsonAsync(ApiRoutes.Notes, new CreateNoteRequest(text), Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<NoteDto>(Ct))!;
    }

    private sealed record ProblemBody(string Title);

    private sealed class ThrowingRepository : INoteRepository
    {
        private readonly string _message;

        public ThrowingRepository(string message)
        {
            _message = message;
        }

        public Task AddAsync(Note note, CancellationToken cancellationToken) => throw new InvalidOperationException(_message);

        public Task<IReadOnlyList<Note>> ListAsync(CancellationToken cancellationToken) => throw new InvalidOperationException(_message);

        public Task<Note> UpdateTextAsync(Guid id, string text, uint expectedVersion, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(_message);
    }
}
