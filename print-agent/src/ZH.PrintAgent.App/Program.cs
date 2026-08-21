using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http.Json;
using ZH.PrintAgent.App;
using ZH.PrintAgent.Contracts;
using ZH.PrintAgent.Core;
using ZH.PrintAgent.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "ZH Print Agent";
});

var agentOptions = builder.Configuration.GetSection(PrintAgentOptions.SectionName).Get<PrintAgentOptions>()
                   ?? new PrintAgentOptions();

PrintAgentStartupValidator.Validate(agentOptions, builder.Environment.EnvironmentName);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = agentOptions.MaxPayloadBytes;
    options.Listen(IPAddress.Parse(agentOptions.BindHost), agentOptions.Port);
});

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

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
            .WithMethods("GET", "POST", "OPTIONS")
            .WithHeaders("Content-Type", PrintAgentOptions.ApiKeyHeaderName);
    });
});

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
builder.Services.AddSingleton<IPrinterCatalog>(_ => new ConfiguredPrinterCatalog(agentOptions.Printers));
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

    if (!IsAuthorized(context.Request, agentOptions.ApiKey))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "Missing or invalid print agent API key." });
        return;
    }

    await next(context);
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

app.Run();

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
