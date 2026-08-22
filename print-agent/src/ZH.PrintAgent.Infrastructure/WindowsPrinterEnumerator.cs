using ZH.PrintAgent.Contracts;
using ZH.PrintAgent.Core;

namespace ZH.PrintAgent.Infrastructure;

public sealed class WindowsPrinterEnumerator : IWindowsPrinterEnumerator
{
    public Task<IReadOnlyList<DetectedPrinter>> EnumerateAsync(CancellationToken cancellationToken)
    {
        var names = WindowsRawPrinterInterop.EnumeratePrinterNames();
        var defaultName = WindowsRawPrinterInterop.GetDefaultPrinterName();

        IReadOnlyList<DetectedPrinter> printers = names
            .Select(name => new DetectedPrinter
            {
                Name = name,
                IsWindowsDefault = string.Equals(name, defaultName, StringComparison.OrdinalIgnoreCase)
            })
            .ToArray();

        return Task.FromResult(printers);
    }
}
