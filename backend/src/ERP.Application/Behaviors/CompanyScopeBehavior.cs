using ERP.Application.Common;
using ERP.Application.Modules.Companies;
using ERP.Domain.Exceptions;
using MediatR;

namespace ERP.Application.Behaviors;

/// <summary>
/// Valida centralmente contexto tenant + empresa + membership para módulos ERP operativos.
/// ICompanyScopedRequest / IRequiresCompanyContext son la única fuente de verdad para scope de empresa.
/// Decide solo por estado explícito (<see cref="ICurrentCompany.HasCompanyContext"/>) y por el
/// <c>Code</c> del guard — nunca por el texto del error (ZH-SCOPE-ERROR-SEMANTICS-01): UNAUTHORIZED →
/// 401, cualquier otro rechazo → CompanyScopeException (403 COMPANY_SCOPE_FORBIDDEN).
/// </summary>
public sealed class CompanyScopeBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICompanyAccessGuard _accessGuard;
    private readonly ICurrentCompany _company;

    public CompanyScopeBehavior(ICompanyAccessGuard accessGuard, ICurrentCompany company)
    {
        _accessGuard = accessGuard;
        _company = company;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        if (request is IPlatformScopedRequest)
            return await next(cancellationToken);

        if (request is ITenantScopedRequest)
        {
            var subOnly = await _accessGuard.RequireActiveTenantAsync(cancellationToken);
            if (!subOnly.IsSuccess)
                throw Denied(subOnly, CompanyScopeException.TenantInactive());
            return await next(cancellationToken);
        }

        if (!RequiresCompanyScope(request))
        {
            return await next(cancellationToken);
        }

        var subResult = await _accessGuard.RequireActiveTenantAsync(cancellationToken);
        if (!subResult.IsSuccess)
            throw Denied(subResult, CompanyScopeException.TenantInactive());

        if (request is ICompanyScopedRequest scoped && scoped.ExplicitCompanyId is Guid explicitId)
        {
            if (explicitId != _company.CompanyId)
                throw CompanyScopeException.JwtMismatch();

            var explicitAccess = await _accessGuard.RequireMembershipAsync(
                explicitId,
                requireActiveCompany: true,
                cancellationToken
            );
            if (!explicitAccess.IsSuccess)
                throw Denied(
                    explicitAccess,
                    CompanyScopeException.AccessDenied(explicitAccess.Error)
                );
        }
        else if (!_company.HasCompanyContext)
        {
            throw CompanyScopeException.NoCompanyContext();
        }
        else
        {
            var ctx = await _accessGuard.RequireCurrentCompanyAsync(cancellationToken);
            if (!ctx.IsSuccess)
                throw Denied(ctx, CompanyScopeException.AccessDenied(ctx.Error));
        }

        return await next(cancellationToken);
    }

    /// <summary>Sin sesión → 401 (UnauthorizedAccessException); cualquier otro rechazo del guard → <paramref name="scopeDenied"/>.</summary>
    private static Exception Denied<T>(Result<T> guardResult, CompanyScopeException scopeDenied) =>
        guardResult.Code == ApiResponseCodes.Common.Unauthorized
            ? new UnauthorizedAccessException(guardResult.Error)
            : scopeDenied;

    private static bool RequiresCompanyScope(TRequest request) =>
        request is ICompanyScopedRequest or IRequiresCompanyContext;
}
