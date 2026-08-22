using Microsoft.Extensions.Options;
using ZH.PrintAgent.Contracts;
using ZH.PrintAgent.Core;

namespace ZH.PrintAgent.App;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app, string settingsFilePath)
    {
        var group = app.MapGroup("/api/admin");

        group.MapGet("/status", async (
            IOptionsMonitor<PrintAgentOptions> optionsMonitor,
            IPrintJobStore store,
            IPrinterCatalog printers,
            CancellationToken cancellationToken) =>
        {
            var options = optionsMonitor.CurrentValue;
            var readiness = await PrintAgentReadiness.EvaluateAsync(options, store, printers, cancellationToken);
            var configuredPrinters = await printers.ListAsync(cancellationToken);
            var defaultPrinter = configuredPrinters.FirstOrDefault(printer => printer.IsDefault)
                                  ?? configuredPrinters.FirstOrDefault(printer => printer.Enabled);

            return Results.Ok(new
            {
                active = true,
                bindHost = options.BindHost,
                port = options.Port,
                allowLan = options.AllowLan,
                mode = options.AllowLan ? "lan" : "localhost",
                apiKeyConfigured = IsRealApiKey(options.ApiKey),
                setupCompleted = options.SetupCompleted,
                defaultPrinter = defaultPrinter?.Name,
                driver = defaultPrinter?.Driver,
                health = "Healthy",
                ready = readiness.Ready,
                readinessErrors = readiness.Errors,
                dataDirectory = Path.GetFullPath(options.DataDirectory),
                logDirectory = Path.GetFullPath(ResolveLogDirectory(options))
            });
        });

        group.MapGet("/printers/windows", async (
            IWindowsPrinterEnumerator enumerator,
            CancellationToken cancellationToken) =>
        {
            var detected = await enumerator.EnumerateAsync(cancellationToken);
            return Results.Ok(detected);
        });

        group.MapGet("/printers", async (IPrinterCatalog printers, CancellationToken cancellationToken) =>
        {
            return Results.Ok(await printers.ListAsync(cancellationToken));
        });

        group.MapPut("/printers", async (
            IReadOnlyList<PrinterInfo> printers,
            IOptionsMonitor<PrintAgentOptions> optionsMonitor,
            CancellationToken cancellationToken) =>
        {
            var updated = optionsMonitor.CurrentValue with { Printers = printers };
            await PrintAgentSettingsStore.SaveAsync(settingsFilePath, updated, cancellationToken);
            return Results.Ok(printers);
        });

        group.MapPost("/printers/{name}/test-print", async (
            string name,
            PrintJobService jobs,
            CancellationToken cancellationToken) =>
        {
            var request = new SubmitPrintJobRequest
            {
                JobId = $"admin-test-{Guid.NewGuid():N}",
                PrinterName = name,
                Copies = 1,
                Receipt = new ReceiptDocument
                {
                    MerchantName = "ZH Print Agent",
                    HeaderLines = new[] { "Test print" },
                    FooterLines = new[] { "If you can read this, the printer is working." }
                }
            };

            var result = await jobs.SubmitAsync(request, cancellationToken);
            if (!result.Success || result.Job is null)
            {
                return Results.ValidationProblem(result.Errors.ToDictionary(error => error, error => new[] { error }));
            }

            return Results.Accepted($"/print-jobs/{result.Job.JobId}", result.Job.ToResponse(result.Duplicate));
        });

        group.MapPost("/apikey/regenerate", async (
            IOptionsMonitor<PrintAgentOptions> optionsMonitor,
            CancellationToken cancellationToken) =>
        {
            var newKey = PrintAgentSettingsStore.GenerateApiKey();
            var updated = optionsMonitor.CurrentValue with { ApiKey = newKey };
            await PrintAgentSettingsStore.SaveAsync(settingsFilePath, updated, cancellationToken);
            return Results.Ok(new { apiKey = newKey });
        });

        group.MapPost("/setup/complete", async (
            IOptionsMonitor<PrintAgentOptions> optionsMonitor,
            IHostEnvironment environment,
            CancellationToken cancellationToken) =>
        {
            var options = optionsMonitor.CurrentValue;
            var errors = PrintAgentStartupValidator.ValidateSetupCompletion(options, environment.EnvironmentName);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["setup"] = errors.ToArray()
                });
            }

            var updated = options with { SetupCompleted = true };
            await PrintAgentSettingsStore.SaveAsync(settingsFilePath, updated, cancellationToken);
            return Results.Ok(new { setupCompleted = true });
        });

        group.MapGet("/queue", async (PrintJobService jobs, CancellationToken cancellationToken) =>
        {
            var items = await jobs.ListAsync(cancellationToken);
            var summary = items
                .GroupBy(job => job.Status)
                .ToDictionary(g => g.Key.ToString(), g => g.Count());

            return Results.Ok(new
            {
                counts = summary,
                items = items.Select(job => job.ToResponse(duplicate: false))
            });
        });

        group.MapPost("/queue/{jobId}/retry", async (string jobId, PrintJobService jobs, CancellationToken cancellationToken) =>
        {
            var job = await jobs.RetryAsync(jobId, cancellationToken);
            return job is null ? Results.NotFound() : Results.Ok(job.ToResponse(duplicate: false));
        });

        group.MapPost("/queue/{jobId}/cancel", async (string jobId, PrintJobService jobs, CancellationToken cancellationToken) =>
        {
            var job = await jobs.CancelAsync(jobId, cancellationToken);
            return job is null ? Results.NotFound() : Results.Ok(job.ToResponse(duplicate: false));
        });

        group.MapPost("/queue/{jobId}/mark-reviewed", async (string jobId, PrintJobService jobs, CancellationToken cancellationToken) =>
        {
            var job = await jobs.MarkReviewedAsync(jobId, cancellationToken);
            return job is null ? Results.NotFound() : Results.Ok(job.ToResponse(duplicate: false));
        });
    }

    private static bool IsRealApiKey(string apiKey)
    {
        return !string.IsNullOrWhiteSpace(apiKey) &&
               !string.Equals(apiKey, PrintAgentStartupValidator.DevelopmentApiKey, StringComparison.Ordinal) &&
               !string.Equals(apiKey, PrintAgentStartupValidator.SampleProductionApiKey, StringComparison.Ordinal);
    }

    private static string ResolveLogDirectory(PrintAgentOptions options)
    {
        return Path.IsPathRooted(options.LogDirectory)
            ? options.LogDirectory
            : Path.Combine(options.DataDirectory, options.LogDirectory);
    }
}
