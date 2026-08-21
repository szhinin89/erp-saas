using ZH.PrintAgent.App;
using ZH.PrintAgent.Contracts;

namespace ZH.PrintAgent.App.Tests;

public sealed class PrintAgentStartupValidatorTests
{
    [Fact]
    public void Validate_fails_in_production_when_api_key_is_default()
    {
        var options = ValidProductionOptions() with
        {
            ApiKey = PrintAgentStartupValidator.DevelopmentApiKey
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PrintAgentStartupValidator.Validate(options, "Production"));

        Assert.Contains("ApiKey", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_fails_in_production_when_enabled_printer_is_simulated()
    {
        var options = ValidProductionOptions() with
        {
            Printers = new[]
            {
                new PrinterInfo { Name = "POS-80", Driver = PrinterDrivers.Simulated, Enabled = true }
            }
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PrintAgentStartupValidator.Validate(options, "Production"));

        Assert.Contains("Simulated printers", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_allows_simulated_printers_in_development()
    {
        var options = new PrintAgentOptions
        {
            ApiKey = PrintAgentStartupValidator.DevelopmentApiKey,
            Printers = new[]
            {
                new PrinterInfo { Name = "POS-80", Driver = PrinterDrivers.Simulated, Enabled = true }
            }
        };

        PrintAgentStartupValidator.Validate(options, "Development");
    }

    private static PrintAgentOptions ValidProductionOptions()
    {
        return new PrintAgentOptions
        {
            ApiKey = "cash-register-secret",
            Printers = new[]
            {
                new PrinterInfo { Name = "POS-80", Driver = PrinterDrivers.WindowsRaw, Enabled = true }
            }
        };
    }
}
