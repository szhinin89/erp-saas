using ERP.Application.Common;
using ERP.Application.Common.Security;
using ERP.Application.Modules.Platform.Companies;
using ERP.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Behaviors;

/// <summary>
/// Valida centralmente contexto subscriber + empresa + membership para módulos ERP operativos.
/// Interfaces explícitas son la fuente primaria; namespace-prefix es fallback legacy temporal.
/// </summary>
public sealed class CompanyScopeBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly string[] CompanyScopedNamespacePrefixes =
    [
        "ERP.Application.Sales",
        "ERP.Application.Modules.Sales",
        "ERP.Application.Modules.Inventory",
        "ERP.Application.Inventory",
        "ERP.Application.Products",
        "ERP.Application.Modules.Products",
        "ERP.Application.Modules.Purchasing",
        "ERP.Application.Purchasing",
        "ERP.Application.Modules.Accounting",
        "ERP.Application.Accounting",
        "ERP.Application.Modules.Cash",
        "ERP.Application.Cash",
        "ERP.Application.Modules.Logistics",
        "ERP.Application.Logistics",
        "ERP.Application.Modules.Branches",
        "ERP.Application.Modules.Expenses",
        "ERP.Application.Modules.Configuration",
    ];

    private readonly ICompanyAccessGuard _accessGuard;
    private readonly ICurrentCompany _company;
    private readonly ICurrentSubscriber _subscriber;
    private readonly ISecurityMetrics _metrics;
    private readonly ILogger<CompanyScopeBehavior<TRequest, TResponse>> _logger;

    public CompanyScopeBehavior(
        ICompanyAccessGuard accessGuard,
        ICurrentSubscriber subscriber,
        ICurrentCompany company,
        ICurrentUser user,
        ISecurityMetrics metrics,
        ILogger<CompanyScopeBehavior<TRequest, TResponse>> logger)
    {
        _accessGuard = accessGuard;
        _company = company;
        _subscriber = subscriber;
        _metrics = metrics;
        _logger = logger;
        _ = user;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is IPlatformScopedRequest)
            return await next();

        if (request is ISubscriberScopedRequest)
        {
            var subOnly = await _accessGuard.RequireActiveSubscriberAsync(ct);
            if (!subOnly.IsSuccess)
                throw CompanyScopeException.SubscriberInactive();
            return await next();
        }

        if (!RequiresCompanyScope(request, out var usedNamespaceFallback))
        {
            return await next();
        }

        if (usedNamespaceFallback)
        {
            _logger.LogWarning(
                "CompanyScopeBehavior: {RequestType} depende solo del namespace-prefix (deuda legacy). " +
                "Migrar a ICompanyScopedRequest.",
                typeof(TRequest).FullName);
            _metrics.RecordNamespaceFallbackUsed(new SecurityMetricTags(
                SubscriberId: _subscriber.SubscriberId,
                CompanyId: _company.CompanyId,
                RequestType: typeof(TRequest).Name));
        }

        var subResult = await _accessGuard.RequireActiveSubscriberAsync(ct);
        if (!subResult.IsSuccess)
            throw CompanyScopeException.SubscriberInactive();

        if (request is ICompanyScopedRequest scoped && scoped.ExplicitCompanyId is Guid explicitId)
        {
            if (explicitId != _company.CompanyId)
                throw CompanyScopeException.JwtMismatch();

            var explicitAccess = await _accessGuard.RequireMembershipAsync(explicitId, requireActiveCompany: true, ct);
            if (!explicitAccess.IsSuccess)
                throw CompanyScopeException.AccessDenied(explicitAccess.Error);
        }
        else
        {
            var ctx = await _accessGuard.RequireCurrentCompanyAsync(ct);
            if (!ctx.IsSuccess)
                throw ctx.Error?.Contains("empresa operativa", StringComparison.OrdinalIgnoreCase) == true
                    ? CompanyScopeException.NoCompanyContext()
                    : CompanyScopeException.AccessDenied(ctx.Error);
        }

        return await next();
    }

    private static bool RequiresCompanyScope(TRequest request, out bool usedNamespaceFallback)
    {
        usedNamespaceFallback = false;

        if (request is ICompanyScopedRequest or IRequiresCompanyContext)
            return true;

        var ns = request.GetType().Namespace ?? string.Empty;
        if (!CompanyScopedNamespacePrefixes.Any(p => ns.StartsWith(p, StringComparison.Ordinal)))
            return false;

        usedNamespaceFallback = true;
        return true;
    }
}
