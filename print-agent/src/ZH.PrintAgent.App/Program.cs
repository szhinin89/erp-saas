using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using ZH.PrintAgent.App;
using ZH.PrintAgent.App.Logging;
using ZH.PrintAgent.Contracts;
using ZH.PrintAgent.Core;
using ZH.PrintAgent.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "ZH Print Agent";
});

var bootstrapOptions = builder.Configuration.GetSection(PrintAgentOptions.SectionName).Get<PrintAgentOptions>()
                        ?? new PrintAgentOptions();

var settingsFilePath = Path.Combine(bootstrapOptions.DataDirectory, "config", "settings.json");
await PrintAgentSettingsStore.EnsureCreatedAsync(settingsFilePath, bootstrapOptions);

builder.Configuration.AddJsonFile(settingsFilePath, optional: true, reloadOnChange: true);

var agentOptions = builder.Configuration.GetSection(PrintAgentOptions.SectionName).Get<PrintAgentOptions>()
                    ?? new PrintAgentOptions();

PrintAgentStartupValidator.Validate(agentOptions, builder.Environment.EnvironmentName);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = agentOptions.MaxPayloadBytes;
    options.Listen(IPAddress.Parse(agentOptions.BindHost), agentOptions.Port);
});

var logDirectory = Path.IsPathRooted(agentOptions.LogDirectory)
    ? agentOptions.LogDirectory
    : Path.Combine(agentOptions.DataDirectory, agentOptions.LogDirectory);
LogRetention.PruneOldLogs(logDirectory, agentOptions.LogRetentionDays);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Logging.AddProvider(new FileLoggerProvider(logDirectory));

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("local-only", policy =>
    {
        policy
            .WithOrigins(agentOptions.AllowedCorsOrigins.ToArray())
            .WithMethods("GET", "POST", "PUT", "OPTIONS")
            .WithHeaders("Content-Type", PrintAgentOptions.ApiKeyHeaderName);
    });
});

builder.Services.Configure<PrintAgentOptions>(builder.Configuration.GetSection(PrintAgentOptions.SectionName));
builder.Services.AddSingleton(agentOptions);
builder.Services.AddSingleton<ISystemClock, SystemClock>();
builder.Services.AddSingleton(new PrintProcessingOptions
{
    MaxAttempts = agentOptions.MaxAttempts,
    BaseRetryDelay = TimeSpan.FromSeconds(agentOptions.BaseRetryDelaySeconds),
    ProcessingStaleAfter = TimeSpan.FromSeconds(agentOptions.ProcessingStaleAfterSeconds)
});
builder.Services.AddSingleton<IPrintJobStore>(_ =>
    new JsonPrintJobStore(Path.Combine(agentOptions.DataDirectory, "queue", "print-jobs.json")));
builder.Services.AddSingleton<ReceiptFormatter>();
builder.Services.AddSingleton<IPrinterLockProvider, KeyedSemaphorePrinterLockProvider>();
builder.Services.AddSingleton<IPrinterCatalog>(sp =>
    new ConfiguredPrinterCatalog(() =>
        sp.GetRequiredService<IOptionsMonitor<PrintAgentOptions>>().CurrentValue.Printers));
builder.Services.AddSingleton<IWindowsPrinterEnumerator, WindowsPrinterEnumerator>();
builder.Services.AddSingleton(_ =>
    new SimulatedReceiptPrinter(
        Path.Combine(agentOptions.DataDirectory, "printed"),
        agentOptions.FailingPrinters));
builder.Services.AddSingleton<WindowsRawReceiptPrinter>();
builder.Services.AddSingleton<IReceiptPrinter, RoutingReceiptPrinter>();
builder.Services.AddSingleton<PrintJobService>();
builder.Services.AddSingleton<PrintJobProcessor>();
builder.Services.AddHostedService<PrintWorkerHostedService>();

var app = builder.Build();

app.UseCors("local-only");

var optionsMonitor = app.Services.GetRequiredService<IOptionsMonitor<PrintAgentOptions>>();

app.Use(async (context, next) =>
{
    if (context.Request.ContentLength > agentOptions.MaxPayloadBytes)
    {
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        await context.Response.WriteAsJsonAsync(new { error = "Payload too large." });
        return;
    }

    if (HttpMethods.IsOptions(context.Request.Method))
    {
        await next(context);
        return;
    }

    var path = context.Request.Path;
    if (IsAdminBootstrapExempt(path, optionsMonitor.CurrentValue))
    {
        await next(context);
        return;
    }

    if (!IsAuthorized(context.Request, optionsMonitor.CurrentValue.ApiKey))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "Missing or invalid print agent API key." });
        return;
    }

    await next(context);
});

var adminWebRoot = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "admin");
Directory.CreateDirectory(adminWebRoot);
var adminFileProvider = new PhysicalFileProvider(adminWebRoot);
app.UseDefaultFiles(new DefaultFilesOptions
{
    RequestPath = "/admin",
    FileProvider = adminFileProvider
});
app.UseStaticFiles(new StaticFileOptions
{
    RequestPath = "/admin",
    FileProvider = adminFileProvider
});

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "ZH.PrintAgent",
    time = DateTimeOffset.UtcNow
}));

app.MapGet("/health/ready", PrintAgentReadiness.CheckAsync);

app.MapPost("/print-jobs", async (
    SubmitPrintJobRequest request,
    PrintJobService jobs,
    CancellationToken cancellationToken) =>
{
    var result = await jobs.SubmitAsync(request, cancellationToken);
    if (!result.Success || result.Job is null)
    {
        return Results.ValidationProblem(result.Errors.ToDictionary(error => error, error => new[] { error }));
    }

    var response = result.Job.ToResponse(result.Duplicate);
    return result.Duplicate
        ? Results.Ok(response)
        : Results.Accepted($"/print-jobs/{result.Job.JobId}", response);
});

app.MapGet("/print-jobs", async (PrintJobService jobs, CancellationToken cancellationToken) =>
{
    var allJobs = await jobs.ListAsync(cancellationToken);
    return Results.Ok(allJobs.Select(job => job.ToResponse(duplicate: false)));
});

app.MapGet("/print-jobs/{jobId}", async (string jobId, PrintJobService jobs, CancellationToken cancellationToken) =>
{
    var job = await jobs.GetAsync(jobId, cancellationToken);
    return job is null
        ? Results.NotFound()
        : Results.Ok(job.ToResponse(duplicate: false));
});

app.MapPost("/print-jobs/{jobId}/cancel", async (string jobId, PrintJobService jobs, CancellationToken cancellationToken) =>
{
    var job = await jobs.CancelAsync(jobId, cancellationToken);
    return job is null
        ? Results.NotFound()
        : Results.Ok(job.ToResponse(duplicate: false));
});

app.MapPost("/print-jobs/{jobId}/retry", async (string jobId, PrintJobService jobs, CancellationToken cancellationToken) =>
{
    var job = await jobs.RetryAsync(jobId, cancellationToken);
    return job is null
        ? Results.NotFound()
        : Results.Ok(job.ToResponse(duplicate: false));
});

app.MapGet("/printers", async (IPrinterCatalog printers, CancellationToken cancellationToken) =>
{
    return Results.Ok(await printers.ListAsync(cancellationToken));
});

app.MapGet("/printers/config", async (
    IPrinterCatalog printers,
    PrintAgentOptions options,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(new PrinterConfigurationResponse
    {
        BindHost = options.BindHost,
        Port = options.Port,
        AllowLan = options.AllowLan,
        MaxPayloadBytes = options.MaxPayloadBytes,
        Printers = await printers.ListAsync(cancellationToken)
    });
});

app.MapAdminEndpoints(settingsFilePath);

await app.RunAsync();

static bool IsAdminBootstrapExempt(PathString path, PrintAgentOptions options)
{
    if (!path.StartsWithSegments("/admin") && !path.StartsWithSegments("/api/admin"))
    {
        return false;
    }

    if (options.SetupCompleted || options.AllowLan)
    {
        return false;
    }

    return IPAddress.TryParse(options.BindHost, out var address) && IPAddress.IsLoopback(address);
}

static bool IsAuthorized(HttpRequest request, string apiKey)
{
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        return false;
    }

    if (!request.Headers.TryGetValue(PrintAgentOptions.ApiKeyHeaderName, out var provided))
    {
        return false;
    }

    var expectedBytes = Encoding.UTF8.GetBytes(apiKey);
    var providedBytes = Encoding.UTF8.GetBytes(provided.ToString());
    return expectedBytes.Length == providedBytes.Length &&
           CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
}

public partial class Program;
