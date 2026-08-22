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

    [Fact]
    public void Validate_allows_bootstrap_mode_in_production_on_loopback()
    {
        var options = new PrintAgentOptions
        {
            SetupCompleted = false,
            AllowLan = false,
            BindHost = "127.0.0.1",
            ApiKey = PrintAgentStartupValidator.SampleProductionApiKey,
            Printers = Array.Empty<PrinterInfo>()
        };

        PrintAgentStartupValidator.Validate(options, "Production");
    }

    [Fact]
    public void Validate_fails_bootstrap_mode_in_production_when_lan_allowed()
    {
        var options = new PrintAgentOptions
        {
            SetupCompleted = false,
            AllowLan = true,
            BindHost = "0.0.0.0",
            ApiKey = PrintAgentStartupValidator.SampleProductionApiKey,
            Printers = Array.Empty<PrinterInfo>()
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PrintAgentStartupValidator.Validate(options, "Production"));

        Assert.Contains("ApiKey", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateSetupCompletion_returns_errors_instead_of_throwing()
    {
        var options = new PrintAgentOptions
        {
            SetupCompleted = false,
            AllowLan = false,
            BindHost = "127.0.0.1",
            ApiKey = PrintAgentStartupValidator.SampleProductionApiKey,
            Printers = Array.Empty<PrinterInfo>()
        };

        var errors = PrintAgentStartupValidator.ValidateSetupCompletion(options, "Production");

        Assert.NotEmpty(errors);
        Assert.Contains(errors, error => error.Contains("ApiKey", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateSetupCompletion_succeeds_with_real_key_and_printer()
    {
        var errors = PrintAgentStartupValidator.ValidateSetupCompletion(ValidProductionOptions(), "Production");

        Assert.Empty(errors);
    }

    private static PrintAgentOptions ValidProductionOptions()
    {
        return new PrintAgentOptions
        {
            SetupCompleted = true,
            ApiKey = "cash-register-secret",
            Printers = new[]
            {
                new PrinterInfo { Name = "POS-80", Driver = PrinterDrivers.WindowsRaw, Enabled = true }
            }
        };
    }
}
