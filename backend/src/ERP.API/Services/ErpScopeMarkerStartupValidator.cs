using ERP.Application.Common;
using MediatR;
using System.Reflection;

namespace ERP.API.Services;

/// <summary>
/// Al arranque, lista requests ERP operativos sin marcador de scope explícito (preparación para eliminar namespace fallback).
/// </summary>
public sealed class ErpScopeMarkerStartupValidator : IHostedService
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

    public ErpScopeMarkerStartupValidator(ILogger<ErpScopeMarkerStartupValidator> logger)
        => _logger = logger;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var appAssembly = typeof(ICurrentSubscriber).Assembly;
        var missing = appAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => typeof(IBaseRequest).IsAssignableFrom(t))
            .Where(t => OperationalPrefixes.Any(p => (t.Namespace ?? "").StartsWith(p, StringComparison.Ordinal)))
            .Where(t => !HasMarker(t))
            .Select(t => t.FullName)
            .ToList();

        if (missing.Count > 0)
        {
            _logger.LogWarning(
                "Startup scope audit: {Count} request(s) ERP sin marcador explícito: {Types}",
                missing.Count,
                string.Join(", ", missing.Take(10)));
        }
        else
        {
            _logger.LogInformation("Startup scope audit: todos los requests ERP operativos tienen marcador explícito.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static bool HasMarker(Type t) =>
        typeof(ICompanyScopedRequest).IsAssignableFrom(t)
        || typeof(ISubscriberScopedRequest).IsAssignableFrom(t)
        || typeof(IPlatformScopedRequest).IsAssignableFrom(t);
}
