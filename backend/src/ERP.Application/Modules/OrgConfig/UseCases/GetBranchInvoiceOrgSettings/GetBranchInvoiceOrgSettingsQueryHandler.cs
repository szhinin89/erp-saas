using ERP.Application.Common;
using ERP.Application.Modules.Branches;
using ERP.Application.Modules.OrgConfig.DTOs;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using MediatR;

namespace ERP.Application.Modules.OrgConfig.UseCases.GetBranchInvoiceOrgSettings;

public sealed class GetBranchInvoiceOrgSettingsQueryHandler
    : IRequestHandler<GetBranchInvoiceOrgSettingsQuery, Result<BranchInvoiceOrgSettingsDto>>
{
    private readonly IOrgSettingsRepository _repo;
    private readonly IBranchAccessGuard _branchAccess;

    public GetBranchInvoiceOrgSettingsQueryHandler(
        IOrgSettingsRepository repo,
        IBranchAccessGuard branchAccess
    )
    {
        _repo = repo;
        _branchAccess = branchAccess;
    }

    public async Task<Result<BranchInvoiceOrgSettingsDto>> Handle(
        GetBranchInvoiceOrgSettingsQuery request,
        CancellationToken cancellationToken
    )
    {
        // BRANCH-CONFIG-CROSS-BRANCH-ACCESS-01: la sucursal de la ruta se autoriza con la misma
        // política que la sucursal activa (empresa, activa, CompanyUserBranch u operador global);
        // BranchScopeBehavior solo valida X-Branch-Id. 404 ajena/inexistente, 403 sin acceso.
        var access = await _branchAccess.RequireBranchAsync(request.BranchId, cancellationToken);
        if (!access.IsSuccess)
            return Result<BranchInvoiceOrgSettingsDto>.Failure(access.Error!, access.Code);

        var tenantId = access.Value!.TenantId;
        var companyId = access.Value.CompanyId;

        var all = await _repo.GetAllForScopeAsync(
            tenantId,
            companyId,
            OrgScope.Branch,
            request.BranchId,
            cancellationToken
        );

        var lookup = all.ToDictionary(s => s.Key, s => s.Value);

        Guid? TryParseGuid(string? value) => Guid.TryParse(value, out var g) ? g : null;

        return Result<BranchInvoiceOrgSettingsDto>.Success(
            new BranchInvoiceOrgSettingsDto(
                DefaultWarehouseId: TryParseGuid(
                    lookup.GetValueOrDefault(OrgSettingKeys.Invoice.DefaultWarehouseId)
                )
            )
        );
    }
}
