namespace ZH.PrintAgent.App.Logging;

public sealed class FileLogger : ILogger
{
    private readonly string categoryName;
    private readonly string logDirectory;
    private readonly object writeLock;

    public FileLogger(string categoryName, string logDirectory, object writeLock)
    {
        this.categoryName = categoryName;
        this.logDirectory = logDirectory;
        this.writeLock = writeLock;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var line = $"{DateTimeOffset.UtcNow:O} [{logLevel}] {categoryName}: {formatter(state, exception)}";
        if (exception is not null)
        {
            line += Environment.NewLine + exception;
        }

        var filePath = Path.Combine(logDirectory, $"printagent-{DateTimeOffset.UtcNow:yyyy-MM-dd}.log");

        lock (writeLock)
        {
            try
            {
                File.AppendAllText(filePath, line + Environment.NewLine);
            }
            catch (IOException)
            {
                // Best-effort file logging; never let a write failure take down the process.
            }
        }
    }
}
