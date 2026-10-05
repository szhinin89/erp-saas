using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ERP.API.Tests.Support;

/// <summary>
/// Logger de pruebas que conserva cada entrada (nombre de evento, mensaje formateado, valores
/// estructurados y excepción) para afirmar que un secreto no aparece en el log.
/// </summary>
public sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<string> _entries = new();

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        var values = state is IEnumerable<KeyValuePair<string, object?>> pairs
            ? string.Join(";", pairs.Select(p => $"{p.Key}={p.Value}"))
            : string.Empty;
        _entries.Enqueue($"{eventId.Name}|{formatter(state, exception)}|{values}|{exception}");
    }

    public string AllText() => string.Join("\n", _entries);
}
