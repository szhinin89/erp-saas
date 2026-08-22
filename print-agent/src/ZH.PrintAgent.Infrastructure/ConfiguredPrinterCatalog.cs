using ZH.PrintAgent.Contracts;
using ZH.PrintAgent.Core;

namespace ZH.PrintAgent.Infrastructure;

public sealed class ConfiguredPrinterCatalog : IPrinterCatalog
{
    private readonly Func<IReadOnlyList<PrinterInfo>> printersAccessor;

    public ConfiguredPrinterCatalog(Func<IReadOnlyList<PrinterInfo>> printersAccessor)
    {
        this.printersAccessor = printersAccessor;
    }

    public Task<IReadOnlyList<PrinterInfo>> ListAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(printersAccessor());
    }

    public Task<PrinterInfo?> FindEnabledAsync(string printerName, CancellationToken cancellationToken)
    {
        var printers = printersAccessor();
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
