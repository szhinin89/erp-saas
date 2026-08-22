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

        foreach (var error in ValidateProductionApiKeyErrors(options, environmentName)
                     .Concat(ValidatePrinterConfigurationErrors(options, environmentName)))
        {
            throw new InvalidOperationException(error);
        }
    }

    /// <summary>
    /// Runs the same rules as <see cref="Validate"/> for the printer/API-key configuration, but returns
    /// error strings instead of throwing, so the setup-completion endpoint can surface them as a 400
    /// instead of letting an incomplete wizard produce a config that crash-loops on next restart.
    /// </summary>
    public static IReadOnlyList<string> ValidateSetupCompletion(PrintAgentOptions options, string environmentName)
    {
        var asCompleted = options with { SetupCompleted = true };
        return ValidateProductionApiKeyErrors(asCompleted, environmentName)
            .Concat(ValidatePrinterConfigurationErrors(asCompleted, environmentName))
            .ToArray();
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

    private static IEnumerable<string> ValidateProductionApiKeyErrors(PrintAgentOptions options, string environmentName)
    {
        if (!IsProduction(environmentName))
        {
            yield break;
        }

        var isDefaultKey =
            string.IsNullOrWhiteSpace(options.ApiKey) ||
            string.Equals(options.ApiKey, DevelopmentApiKey, StringComparison.Ordinal) ||
            string.Equals(options.ApiKey, SampleProductionApiKey, StringComparison.Ordinal);

        if (!isDefaultKey)
        {
            yield break;
        }

        if (!options.SetupCompleted)
        {
            // Bootstrap mode: allow booting with the sentinel key so the local /admin wizard can run,
            // but only while the instance is not reachable from outside this machine.
            if (options.AllowLan || !IPAddress.TryParse(options.BindHost, out var address) || !IPAddress.IsLoopback(address))
            {
                yield return "PrintAgent:ApiKey must be changed before running in Production with AllowLan enabled or a non-loopback BindHost.";
            }

            yield break;
        }

        yield return "PrintAgent:ApiKey must be changed before running in Production.";
    }

    private static IEnumerable<string> ValidatePrinterConfigurationErrors(PrintAgentOptions options, string environmentName)
    {
        if (!options.SetupCompleted)
        {
            // The setup wizard is what configures printers - don't crash-loop before it can run.
            yield break;
        }

        var enabledPrinters = options.Printers.Where(printer => printer.Enabled).ToArray();
        if (IsProduction(environmentName) && enabledPrinters.Length == 0)
        {
            yield return "At least one enabled printer must be configured in Production.";
        }

        foreach (var printer in enabledPrinters)
        {
            if (!PrinterDrivers.IsSupported(printer.Driver))
            {
                yield return $"Printer '{printer.Name}' uses unsupported driver '{printer.Driver}'.";
                continue;
            }

            if (!AllowsSimulatedPrinters(environmentName) &&
                string.Equals(printer.Driver, PrinterDrivers.Simulated, StringComparison.OrdinalIgnoreCase))
            {
                yield return "Simulated printers are allowed only in Development/Test environments.";
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
