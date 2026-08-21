using ZH.PrintAgent.Contracts;
using ZH.PrintAgent.Core;

namespace ZH.PrintAgent.Infrastructure;

public sealed class RoutingReceiptPrinter : IReceiptPrinter
{
    private readonly IPrinterCatalog printerCatalog;
    private readonly SimulatedReceiptPrinter simulatedPrinter;
    private readonly WindowsRawReceiptPrinter windowsRawPrinter;

    public RoutingReceiptPrinter(
        IPrinterCatalog printerCatalog,
        SimulatedReceiptPrinter simulatedPrinter,
        WindowsRawReceiptPrinter windowsRawPrinter)
    {
        this.printerCatalog = printerCatalog;
        this.simulatedPrinter = simulatedPrinter;
        this.windowsRawPrinter = windowsRawPrinter;
    }

    public async Task PrintAsync(PrintJob job, string formattedReceipt, CancellationToken cancellationToken)
    {
        var printer = await printerCatalog.FindEnabledAsync(job.PrinterName, cancellationToken);
        if (printer is null)
        {
            throw new InvalidOperationException($"Printer '{job.PrinterName}' is not configured or is disabled.");
        }

        if (string.Equals(printer.Driver, PrinterDrivers.Simulated, StringComparison.OrdinalIgnoreCase))
        {
            await simulatedPrinter.PrintAsync(job, formattedReceipt, cancellationToken);
            return;
        }

        if (string.Equals(printer.Driver, PrinterDrivers.WindowsRaw, StringComparison.OrdinalIgnoreCase))
        {
            await windowsRawPrinter.PrintAsync(job, formattedReceipt, cancellationToken);
            return;
        }

        throw new InvalidOperationException($"Printer '{job.PrinterName}' uses unsupported driver '{printer.Driver}'.");
    }
}
