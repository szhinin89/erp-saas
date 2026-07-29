using ERP.Application.Common;
using MediatR;

namespace ERP.API.Services;

/// <summary>
/// Al arranque, lista requests ERP operativos sin marcador de scope explícito (preparación para eliminar namespace fallback).
/// </summary>
public sealed partial class ErpScopeMarkerStartupValidator : IHostedService
{
    private static readonly string[] OperationalPrefixes =
    [
        "ERP.Application.Modules.Sales",
        "ERP.Application.Sales",
        "ERP.Application.Modules.Purchasing",
        "ERP.Application.Purchasing",
        "ERP.Application.Modules.Inventory",
        "ERP.Application.Modules.Accounting",
        "ERP.Application.Modules.Logistics",
        "ERP.Application.Modules.Products",
        "ERP.Application.Modules.Cash",
        "ERP.Application.Modules.Branches",
        "ERP.Application.Modules.Expenses",
    ];

    private readonly ILogger<ErpScopeMarkerStartupValidator> _logger;

    public ErpScopeMarkerStartupValidator(ILogger<ErpScopeMarkerStartupValidator> logger) =>
        _logger = logger;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var appAssembly = typeof(ICurrentTenant).Assembly;
        var missing = appAssembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => typeof(IBaseRequest).IsAssignableFrom(t))
            .Where(t =>
                OperationalPrefixes.Any(p =>
                    (t.Namespace ?? "").StartsWith(p, StringComparison.Ordinal)
                )
            )
            .Where(t => !HasMarker(t))
            .Select(t => t.FullName)
            .ToList();

        if (missing.Count > 0)
        {
            LogScopeMissing(missing.Count, string.Join(", ", missing.Take(10)));
        }
        else
        {
            LogScopeAuditOk();
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static bool HasMarker(Type t) =>
        typeof(ICompanyScopedRequest).IsAssignableFrom(t)
        || typeof(ITenantScopedRequest).IsAssignableFrom(t)
        || typeof(IPlatformScopedRequest).IsAssignableFrom(t);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Startup scope audit: {Count} request(s) ERP sin marcador explícito: {Types}"
    )]
    private partial void LogScopeMissing(int count, string types);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Startup scope audit: todos los requests ERP operativos tienen marcador explícito."
    )]
    private partial void LogScopeAuditOk();
}
