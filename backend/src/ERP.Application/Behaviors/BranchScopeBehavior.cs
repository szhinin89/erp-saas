using ERP.Application.Common;
using ERP.Application.Modules.Branches;
using ERP.Domain.Exceptions;
using MediatR;

namespace ERP.Application.Behaviors;

/// <summary>
/// Valida centralmente el contexto de sucursal para módulos ERP que operan por sucursal.
/// IBranchScopedRequest es la única fuente de verdad para exigir sucursal — este behavior solo
/// orquesta (obtiene ICurrentBranch, verifica que exista contexto, invoca exclusivamente
/// IBranchAccessGuard); toda la regla de negocio (existencia, estado, empresa,
/// CompanyUserBranch) vive únicamente en IBranchAccessGuard. Se registra después de
/// CompanyScopeBehavior (orden de pipeline: Tenant → Company → Branch) — IBranchScopedRequest
/// hereda de ICompanyScopedRequest, así que CompanyScopeBehavior ya valida empresa operativa
/// antes de que este behavior se ejecute.
/// </summary>
public sealed class BranchScopeBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IBranchAccessGuard _accessGuard;
    private readonly ICurrentBranch _branch;

    public BranchScopeBehavior(IBranchAccessGuard accessGuard, ICurrentBranch branch)
    {
        _accessGuard = accessGuard;
        _branch = branch;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        if (request is not IBranchScopedRequest)
            return await next(cancellationToken);

        if (!_branch.HasBranchContext)
            throw BranchScopeException.NoBranchContext();

        // La traducción contexto → código vive en RequireCurrentBranchAsync; aquí solo se elige la
        // excepción por Code (nunca por el texto): 401 sin sesión, 403 COMPANY_SCOPE_FORBIDDEN si
        // falló la empresa, 403 BRANCH_SCOPE_FORBIDDEN para todo rechazo de la sucursal.
        var result = await _accessGuard.RequireCurrentBranchAsync(cancellationToken);
        if (!result.IsSuccess)
            throw result.Code switch
            {
                ApiResponseCodes.Common.Unauthorized => new UnauthorizedAccessException(
                    result.Error
                ),
                ApiResponseCodes.Common.CompanyScopeForbidden => CompanyScopeException.AccessDenied(
                    result.Error
                ),
                _ => BranchScopeException.AccessDenied(result.Error),
            };

        return await next(cancellationToken);
    }
}
