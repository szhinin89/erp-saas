using System.Net;
using ZH.PrintAgent.Contracts;

namespace ZH.PrintAgent.App;

public static class PrintAgentStartupValidator
{
    public const string DevelopmentApiKey = "local-dev-key-change-me";
    public const string SampleProductionApiKey = "replace-with-cash-register-local-secret";

    public static void Validate(PrintAgentOptions options, string environmentName)
    {
        ValidateBindOptions(options);
        ValidateProductionApiKey(options, environmentName);
        ValidatePrinterConfiguration(options, environmentName);
    }

    private static void ValidateBindOptions(PrintAgentOptions options)
    {
        if (!IPAddress.TryParse(options.BindHost, out var address))
        {
            throw new InvalidOperationException("PrintAgent:BindHost must be a valid IP address.");
        }

        if (!options.AllowLan && !IPAddress.IsLoopback(address))
        {
            throw new InvalidOperationException("LAN binding is disabled. Set PrintAgent:AllowLan=true to bind outside localhost.");
        }
    }

    private static void ValidateProductionApiKey(PrintAgentOptions options, string environmentName)
    {
        if (!IsProduction(environmentName))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey) ||
            string.Equals(options.ApiKey, DevelopmentApiKey, StringComparison.Ordinal) ||
            string.Equals(options.ApiKey, SampleProductionApiKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("PrintAgent:ApiKey must be changed before running in Production.");
        }
    }

    private static void ValidatePrinterConfiguration(PrintAgentOptions options, string environmentName)
    {
        var enabledPrinters = options.Printers.Where(printer => printer.Enabled).ToArray();
        if (IsProduction(environmentName) && enabledPrinters.Length == 0)
        {
            throw new InvalidOperationException("At least one enabled printer must be configured in Production.");
        }

        foreach (var printer in enabledPrinters)
        {
            if (!PrinterDrivers.IsSupported(printer.Driver))
            {
                throw new InvalidOperationException(
                    $"Printer '{printer.Name}' uses unsupported driver '{printer.Driver}'.");
            }

            if (!AllowsSimulatedPrinters(environmentName) &&
                string.Equals(printer.Driver, PrinterDrivers.Simulated, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Simulated printers are allowed only in Development/Test environments.");
            }
        }
    }

    private static bool IsProduction(string environmentName)
    {
        return string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);
    }

    private static bool AllowsSimulatedPrinters(string environmentName)
    {
        return string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(environmentName, "Test", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);
    }
}
