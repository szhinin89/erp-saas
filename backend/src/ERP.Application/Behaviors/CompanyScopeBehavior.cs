using ERP.Application.Common;
using ERP.Application.Common.Security;
using ERP.Application.Modules.Platform.Companies;
using ERP.Domain.Exceptions;
using MediatR;

namespace ERP.Application.Behaviors;

/// <summary>
/// Valida centralmente contexto subscriber + empresa + membership para módulos ERP operativos.
/// ICompanyScopedRequest / IRequiresCompanyContext son la única fuente de verdad para scope de empresa.
/// </summary>
public sealed class CompanyScopeBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICompanyAccessGuard _accessGuard;
    private readonly ICurrentCompany     _company;

    public CompanyScopeBehavior(
        ICompanyAccessGuard accessGuard,
        ICurrentCompany company)
    {
        _accessGuard = accessGuard;
        _company     = company;
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

        if (!RequiresCompanyScope(request))
        {
            return await next();
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

    private static bool RequiresCompanyScope(TRequest request)
        => request is ICompanyScopedRequest or IRequiresCompanyContext;
}
