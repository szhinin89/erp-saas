namespace ZH.PrintAgent.App.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string logDirectory;
    private readonly object writeLock = new();

    public FileLoggerProvider(string logDirectory)
    {
        this.logDirectory = logDirectory;
        Directory.CreateDirectory(logDirectory);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(categoryName, logDirectory, writeLock);
    }

    public void Dispose()
    {
    }
}
