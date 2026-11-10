using ERP.Application.Common;
using ERP.Application.Modules.Branches;
using ERP.Application.Modules.OrgConfig.DTOs;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;

namespace ERP.Application.Modules.OrgConfig.UseCases.UpsertBranchInvoiceOrgSettings;

public sealed class UpsertBranchInvoiceOrgSettingsCommandHandler
    : IRequestHandler<UpsertBranchInvoiceOrgSettingsCommand, Result<BranchInvoiceOrgSettingsDto>>
{
    public const string InvalidWarehouseMessage =
        "La bodega seleccionada no existe, está inactiva o no pertenece a esta sucursal.";

    private readonly IOrgSettingsRepository _repo;
    private readonly IBranchAccessGuard _branchAccess;
    private readonly IWarehouseRepository _warehouseRepo;

    public UpsertBranchInvoiceOrgSettingsCommandHandler(
        IOrgSettingsRepository repo,
        IBranchAccessGuard branchAccess,
        IWarehouseRepository warehouseRepo
    )
    {
        _repo = repo;
        _branchAccess = branchAccess;
        _warehouseRepo = warehouseRepo;
    }

    public async Task<Result<BranchInvoiceOrgSettingsDto>> Handle(
        UpsertBranchInvoiceOrgSettingsCommand command,
        CancellationToken cancellationToken
    )
    {
        // BRANCH-CONFIG-CROSS-BRANCH-ACCESS-01: la sucursal de la ruta se autoriza con la misma
        // política que la sucursal activa (empresa, activa, CompanyUserBranch u operador global);
        // BranchScopeBehavior solo valida X-Branch-Id. 404 ajena/inexistente, 403 sin acceso.
        var access = await _branchAccess.RequireBranchAsync(command.BranchId, cancellationToken);
        if (!access.IsSuccess)
            return Result<BranchInvoiceOrgSettingsDto>.Failure(access.Error!, access.Code);

        var tenantId = access.Value!.TenantId;
        var companyId = access.Value.CompanyId;
        var userId = access.Value.UserId;

        // BRANCH-CONFIG-WAREHOUSE-UNSET-01: la bodega se valida también al guardar (además del
        // fail-closed en lectura de InvoiceDefaultsResolver). null = sin configurar → el resolver
        // usa Warehouse.IsMain de la sucursal. Inexistente, de otra empresa, inactiva o de otra
        // sucursal → 422 con un único mensaje (no revela si existe en otra empresa).
        if (command.DefaultWarehouseId is { } warehouseId)
        {
            var warehouse = await _warehouseRepo.GetByIdForCompanyAsync(
                tenantId,
                companyId,
                warehouseId,
                cancellationToken
            );
            if (
                warehouse is null
                || !warehouse.IsActive
                || warehouse.BranchId != command.BranchId
            )
                return Result<BranchInvoiceOrgSettingsDto>.ValidationFailure(
                    InvalidWarehouseMessage
                );
        }

        var setting = OrgSetting.Create(
            tenantId,
            companyId,
            OrgScope.Branch,
            command.BranchId,
            OrgSettingKeys.Invoice.DefaultWarehouseId,
            command.DefaultWarehouseId?.ToString(),
            SettingDataType.Guid,
            userId
        );

        await _repo.UpsertAsync(setting, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);

        return Result<BranchInvoiceOrgSettingsDto>.Success(
            new BranchInvoiceOrgSettingsDto(command.DefaultWarehouseId)
        );
    }
}
