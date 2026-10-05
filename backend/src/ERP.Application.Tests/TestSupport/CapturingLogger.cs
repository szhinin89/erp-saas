using Microsoft.Extensions.Logging;

namespace ERP.Application.Tests.TestSupport;

/// <summary>
/// Logger de pruebas que conserva cada entrada: mensaje formateado, valores estructurados y
/// excepción. Permite afirmar que un secreto no aparece en ninguna parte del log.
/// </summary>
public sealed class CapturingLogger<T> : ILogger<T>
{
    public List<CapturedLogEntry> Entries { get; } = new();

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
            ? pairs.Select(p => $"{p.Key}={p.Value}").ToList()
            : new List<string>();
        Entries.Add(
            new CapturedLogEntry(
                logLevel,
                eventId,
                formatter(state, exception),
                values,
                exception?.ToString()
            )
        );
    }

    /// <summary>Todo el contenido registrado (mensajes, valores y excepciones) como un solo texto.</summary>
    public string AllText() =>
        string.Join(
            "\n",
            Entries.Select(e =>
                $"{e.EventId.Name}|{e.Message}|{string.Join(";", e.Values)}|{e.Exception}"
            )
        );
}

public sealed record CapturedLogEntry(
    LogLevel Level,
    EventId EventId,
    string Message,
    IReadOnlyList<string> Values,
    string? Exception
);
