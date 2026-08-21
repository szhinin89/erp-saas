using ZH.PrintAgent.Contracts;
using ZH.PrintAgent.Core;

namespace ZH.PrintAgent.Infrastructure;

public sealed class ConfiguredPrinterCatalog : IPrinterCatalog
{
    private readonly IReadOnlyList<PrinterInfo> printers;

    public ConfiguredPrinterCatalog(IReadOnlyList<PrinterInfo> printers)
    {
        this.printers = printers;
    }

    public Task<IReadOnlyList<PrinterInfo>> ListAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(printers);
    }

    public Task<PrinterInfo?> FindEnabledAsync(string printerName, CancellationToken cancellationToken)
    {
        var printer = printers.FirstOrDefault(candidate =>
            candidate.Enabled &&
            string.Equals(candidate.Name, printerName, StringComparison.OrdinalIgnoreCase));

        if (printer is not null &&
            string.Equals(printer.Driver, PrinterDrivers.WindowsRaw, StringComparison.OrdinalIgnoreCase) &&
            !WindowsRawPrinterInterop.CanOpenPrinter(printer.Name))
        {
            return Task.FromResult<PrinterInfo?>(null);
        }

        return Task.FromResult(printer);
    }
}
