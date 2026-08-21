using ZH.PrintAgent.Contracts;

namespace ZH.PrintAgent.App;

public sealed record PrintAgentOptions
{
    public const string SectionName = "PrintAgent";
    public const string ApiKeyHeaderName = "X-ZH-PrintAgent-Key";

    public string BindHost { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 9817;
    public bool AllowLan { get; init; }
    public string ApiKey { get; init; } = "local-dev-key-change-me";
    public long MaxPayloadBytes { get; init; } = 65536;
    public string DataDirectory { get; init; } = "data";
    public int PollIntervalMilliseconds { get; init; } = 500;
    public int MaxAttempts { get; init; } = 3;
    public int BaseRetryDelaySeconds { get; init; } = 5;
    public int ProcessingStaleAfterSeconds { get; init; } = 300;
    public IReadOnlyList<string> AllowedCorsOrigins { get; init; } = new[]
    {
        "http://127.0.0.1:5173",
        "http://localhost:5173"
    };
    public IReadOnlyList<string> FailingPrinters { get; init; } = Array.Empty<string>();
    public IReadOnlyList<PrinterInfo> Printers { get; init; } = Array.Empty<PrinterInfo>();
}
