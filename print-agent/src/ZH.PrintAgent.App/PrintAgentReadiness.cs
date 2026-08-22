using ZH.PrintAgent.Contracts;
using ZH.PrintAgent.Core;

namespace ZH.PrintAgent.App;

public sealed record ReadinessResult(bool Ready, IReadOnlyList<string> Errors);

public static class PrintAgentReadiness
{
    public static async Task<IResult> CheckAsync(
        PrintAgentOptions options,
        IPrintJobStore store,
        IPrinterCatalog printers,
        CancellationToken cancellationToken)
    {
        var result = await EvaluateAsync(options, store, printers, cancellationToken);

        var payload = new
        {
            status = result.Ready ? "Ready" : "NotReady",
            service = "ZH.PrintAgent",
            driverMode = options.Printers.Select(printer => new
            {
                printer.Name,
                printer.Driver,
                printer.Enabled
            }),
            time = DateTimeOffset.UtcNow,
            errors = result.Errors
        };

        return result.Ready
            ? Results.Ok(payload)
            : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    public static async Task<ReadinessResult> EvaluateAsync(
        PrintAgentOptions options,
        IPrintJobStore store,
        IPrinterCatalog printers,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        CheckDataDirectory(options, errors);

        try
        {
            await store.ListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            errors.Add($"Print job queue is not readable: {ex.Message}");
        }

        try
        {
            var configuredPrinters = await printers.ListAsync(cancellationToken);
            var enabledPrinters = configuredPrinters.Where(printer => printer.Enabled).ToArray();
            if (enabledPrinters.Length == 0)
            {
                errors.Add("No enabled printers are configured.");
            }

            foreach (var printer in enabledPrinters)
            {
                if (!PrinterDrivers.IsSupported(printer.Driver))
                {
                    errors.Add($"Printer '{printer.Name}' uses unsupported driver '{printer.Driver}'.");
                    continue;
                }

                var readyPrinter = await printers.FindEnabledAsync(printer.Name, cancellationToken);
                if (readyPrinter is null)
                {
                    errors.Add($"Printer '{printer.Name}' is not available for driver '{printer.Driver}'.");
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Printer configuration is not readable: {ex.Message}");
        }

        return new ReadinessResult(errors.Count == 0, errors);
    }

    private static void CheckDataDirectory(PrintAgentOptions options, List<string> errors)
    {
        try
        {
            Directory.CreateDirectory(options.DataDirectory);
            var probePath = Path.Combine(options.DataDirectory, $".ready-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probePath, "ok");
            File.Delete(probePath);
        }
        catch (Exception ex)
        {
            errors.Add($"Data directory '{options.DataDirectory}' is not writable: {ex.Message}");
        }
    }
}
