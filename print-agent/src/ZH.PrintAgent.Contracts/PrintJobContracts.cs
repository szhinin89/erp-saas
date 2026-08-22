namespace ZH.PrintAgent.Contracts;

public sealed record SubmitPrintJobRequest
{
    public string JobId { get; init; } = string.Empty;
    public string PrinterName { get; init; } = string.Empty;
    public int Copies { get; init; } = 1;
    public ReceiptDocument? Receipt { get; init; } = new();
}

public sealed record PrintJobResponse
{
    public string JobId { get; init; } = string.Empty;
    public string PrinterName { get; init; } = string.Empty;
    public PrintJobStatus Status { get; init; }
    public bool Duplicate { get; init; }
    public int Attempts { get; init; }
    public string? LastError { get; init; }
    public bool Reviewed { get; init; }
    public DateTimeOffset? ReviewedAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? NextAttemptAt { get; init; }
}

public sealed record PrinterInfo
{
    public string Name { get; init; } = string.Empty;
    public string Driver { get; init; } = "simulated";
    public bool Enabled { get; init; } = true;
    public bool IsDefault { get; init; }
    public int PaperWidthMm { get; init; } = 80;
}

public static class PrinterDrivers
{
    public const string Simulated = "simulated";
    public const string WindowsRaw = "windows-raw";

    public static bool IsSupported(string driver)
    {
        return string.Equals(driver, Simulated, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(driver, WindowsRaw, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record DetectedPrinter
{
    public string Name { get; init; } = string.Empty;
    public bool IsWindowsDefault { get; init; }
}

public sealed record PrinterConfigurationResponse
{
    public string BindHost { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 9817;
    public bool AllowLan { get; init; }
    public long MaxPayloadBytes { get; init; }
    public IReadOnlyList<PrinterInfo> Printers { get; init; } = Array.Empty<PrinterInfo>();
}
