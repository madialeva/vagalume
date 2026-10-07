using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;
using Vagalume.Desktop.Hosting;
using Vagalume.Host.Desktop;

namespace Vagalume.Host.Desktop.Tests;

[Trait("Category", "Integration")]
public sealed class LocalSessionGuardTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task RequestWithoutToken_IsRejectedWithoutRevealingContent()
    {
        await using var desktop = await RunningDesktop.StartAsync();
        using var client = RunningDesktop.NewClient();

        foreach (var path in new[] { "/", "/_blazor/negotiate?negotiateVersion=1", "/anything" })
        {
            var response = await client.GetAsync(new Uri(new Uri(desktop.Address), path), Ct);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsStringAsync(Ct));
        }
    }

    [Fact]
    public async Task WrongToken_IsRejected()
    {
        await using var desktop = await RunningDesktop.StartAsync();
        using var client = RunningDesktop.NewClient();

        var response = await client.GetAsync(
            new Uri(new Uri(desktop.Address), $"/?{LocalSessionGuard.QueryName}={new string('0', 64)}"), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task WindowAddress_IsExchangedForACookieThatOpensTheWholeUiIncludingTheInteractiveChannel()
    {
        await using var desktop = await RunningDesktop.StartAsync();
        using var client = RunningDesktop.NewClient();

        var landing = await client.GetAsync(desktop.App.StartUrl(desktop.Address), Ct);

        Assert.Equal(HttpStatusCode.Redirect, landing.StatusCode);
        Assert.Equal("/", landing.Headers.Location?.OriginalString);
        var cookie = Assert.Single(landing.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);

        var page = await client.GetAsync(new Uri(desktop.Address), Ct);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);

        var script = await client.GetAsync(new Uri(new Uri(desktop.Address), "/_framework/blazor.web.js"), Ct);
        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
        Assert.True((await script.Content.ReadAsByteArrayAsync(Ct)).Length > 10_000, "the Blazor client script must be served");

        var negotiate = await client.PostAsync(
            new Uri(new Uri(desktop.Address), "/_blazor/negotiate?negotiateVersion=1"), null, Ct);
        Assert.Equal(HttpStatusCode.OK, negotiate.StatusCode);
    }

    [Fact]
    public async Task Token_IsNeverWrittenToTheLogs()
    {
        var logs = new CollectingLoggerProvider();
        await using var desktop = await RunningDesktop.StartAsync(configure: builder =>
        {
            builder.Logging.SetMinimumLevel(LogLevel.Trace);
            builder.Logging.AddProvider(logs);
        });
        using var client = RunningDesktop.NewClient();

        await client.GetAsync(desktop.App.StartUrl(desktop.Address), Ct);
        await client.GetAsync(new Uri(desktop.Address), Ct);

        Assert.NotEmpty(logs.Messages);
        Assert.DoesNotContain(logs.Messages, m => m.Contains(desktop.App.Token.Value, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidToken_WithAForeignHostHeader_IsRejected()
    {
        await using var desktop = await RunningDesktop.StartAsync();
        using var client = RunningDesktop.NewClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, desktop.App.StartUrl(desktop.Address));
        request.Headers.Host = "attacker.example";

        var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TokenChangesOnEveryStart_AndThePreviousOneIsRejected()
    {
        var root = Vagalume.Testing.TestEnvironment.NewTempDirectory();
        try
        {
            string oldToken;
            await using (var first = await RunningDesktop.StartAsync(root))
            {
                oldToken = first.App.Token.Value;
            }

            await using var second = await RunningDesktop.StartAsync(root);
            Assert.NotEqual(oldToken, second.App.Token.Value);
            using var client = RunningDesktop.NewClient();

            var response = await client.GetAsync(
                new Uri(new Uri(second.Address), $"/?{LocalSessionGuard.QueryName}={oldToken}"), Ct);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            Npgsql.NpgsqlConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }
}

/// <summary>
/// Logger provider that keeps every formatted message so a test can search them.
/// </summary>
internal sealed class CollectingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _messages = new();

    public IReadOnlyCollection<string> Messages => _messages;

    public ILogger CreateLogger(string categoryName) => new Collector(_messages);

    public void Dispose()
    {
    }

    private sealed class Collector : ILogger
    {
        private readonly ConcurrentQueue<string> _sink;

        public Collector(ConcurrentQueue<string> sink)
        {
            _sink = sink;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            _sink.Enqueue(formatter(state, exception));
    }
}
