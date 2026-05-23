using ERP.Application.Common;
using ERP.Application.Modules.Platform.Companies;
using ERP.Domain.Exceptions;
using MediatR;

namespace ERP.Application.Behaviors;

/// <summary>
/// Valida centralmente contexto subscriber + empresa + membership + billing para módulos ERP operativos.
/// </summary>
public sealed class CompanyScopeBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly string[] CompanyScopedNamespacePrefixes =
    [
        "ERP.Application.Sales",
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
    ];

    private readonly ICompanyAccessGuard _accessGuard;
    private readonly ICurrentCompany _company;

    public CompanyScopeBehavior(
        ICompanyAccessGuard accessGuard,
        ICurrentSubscriber subscriber,
        ICurrentCompany company,
        ICurrentUser user)
    {
        _accessGuard = accessGuard;
        _company = company;
        _ = subscriber;
        _ = user;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is ISubscriberOnlyRequest)
            return await next();

        if (!RequiresCompanyScope(request))
            return await next();

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

    private static bool RequiresCompanyScope(TRequest request)
    {
        if (request is ICompanyScopedRequest)    return true;
        if (request is IRequiresCompanyContext)  return true;

        var ns = request.GetType().Namespace ?? string.Empty;
        return CompanyScopedNamespacePrefixes.Any(p => ns.StartsWith(p, StringComparison.Ordinal));
    }
}
