using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Vagalume.Host.Wasm.Desktop.Tests;

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
