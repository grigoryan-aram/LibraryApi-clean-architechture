using Microsoft.Extensions.Logging;

namespace Application.UnitTests.TestDoubles;

// Keeps what was written to it so a test can assert on the level and the
// rendered message. Moq can verify ILogger.Log, but only through the
// non-generic TState, which makes both the setup and the failure message
// unreadable.
//
// Most handler tests take NullLogger instead: a log line is usually incidental
// to the behaviour under test. This exists for the cases where what does — or
// must not — reach the log is the point, such as a password never appearing in
// it.
public sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<Entry> _entries = [];

    public IReadOnlyList<Entry> Entries => _entries;

    public IEnumerable<Entry> At(LogLevel level) =>
        _entries.Where(entry => entry.Level == level);

    public bool Mentions(string text) =>
        _entries.Any(entry =>
            entry.Message.Contains(text, StringComparison.OrdinalIgnoreCase));

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        _entries.Add(new Entry(logLevel, formatter(state, exception), exception));

    public record Entry(LogLevel Level, string Message, Exception? Exception);

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
