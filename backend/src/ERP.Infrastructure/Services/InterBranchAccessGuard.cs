using ERP.Application.Common;
using ERP.Application.Modules.Branches;
using ERP.Application.Modules.Companies;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Infrastructure.Services;

public sealed class InterBranchAccessGuard : IInterBranchAccessGuard
{
    private readonly ICompanyAccessGuard _companyAccessGuard;
    private readonly IBranchAccessGuard _branchAccessGuard;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly ICurrentBranch _currentBranch;

    public InterBranchAccessGuard(
        ICompanyAccessGuard companyAccessGuard,
        IBranchAccessGuard branchAccessGuard,
        IWarehouseRepository warehouseRepository,
        ICurrentBranch currentBranch
    )
    {
        _companyAccessGuard = companyAccessGuard;
        _branchAccessGuard = branchAccessGuard;
        _warehouseRepository = warehouseRepository;
        _currentBranch = currentBranch;
    }

    public async Task<Result<InterBranchAccessContext>> RequireInterBranchAccessAsync(
        Guid sourceWarehouseId,
        Guid targetWarehouseId,
        CancellationToken cancellationToken = default
    )
    {
        var companyAccess = await _companyAccessGuard.RequireCurrentCompanyAsync(cancellationToken);
        if (!companyAccess.IsSuccess)
            return Result<InterBranchAccessContext>.Failure(
                companyAccess.Error!,
                companyAccess.Code
            );

        var company = companyAccess.Value!;

        if (!_currentBranch.HasBranchContext)
            return Result<InterBranchAccessContext>.Failure(
                "Debe seleccionar una sucursal activa.",
                ApiResponseCodes.Common.BranchScopeForbidden
            );

        // La sucursal activa se persiste como OperationBranchId: antes solo se exigía el header,
        // nunca se validaba (fail-open). Ahora pasa por la misma validación de contexto que
        // BranchScopeBehavior (existencia, empresa, estado, CompanyUserBranch).
        var operatingBranch = await _branchAccessGuard.RequireCurrentBranchAsync(cancellationToken);
        if (!operatingBranch.IsSuccess)
            return Result<InterBranchAccessContext>.Failure(
                operatingBranch.Error!,
                operatingBranch.Code
            );

        var sourceWarehouse = await _warehouseRepository.GetByIdAsync(
            company.TenantId,
            sourceWarehouseId,
            cancellationToken
        );
        var targetWarehouse = await _warehouseRepository.GetByIdAsync(
            company.TenantId,
            targetWarehouseId,
            cancellationToken
        );

        // Inexistente y de otra empresa del tenant son indistinguibles (mismo NOT_FOUND y mismo
        // texto) y la pertenencia se valida ANTES que el estado: una bodega ajena deshabilitada
        // nunca revela que existe. Antes: "no pertenece a la empresa operativa actual" /
        // "está deshabilitada" permitían enumerar bodegas de otras empresas.
        if (sourceWarehouse is null || sourceWarehouse.CompanyId != company.CompanyId)
            return Result<InterBranchAccessContext>.NotFound("Bodega origen no encontrada.");

        if (targetWarehouse is null || targetWarehouse.CompanyId != company.CompanyId)
            return Result<InterBranchAccessContext>.NotFound("Bodega destino no encontrada.");

        if (!sourceWarehouse.IsActive)
            return Result<InterBranchAccessContext>.ValidationFailure(
                "La bodega origen está deshabilitada."
            );

        if (!targetWarehouse.IsActive)
            return Result<InterBranchAccessContext>.ValidationFailure(
                "La bodega destino está deshabilitada."
            );

        var sourceBranchAccess = await _branchAccessGuard.RequireBranchAsync(
            sourceWarehouse.BranchId,
            cancellationToken
        );
        if (!sourceBranchAccess.IsSuccess)
            return Result<InterBranchAccessContext>.Failure(
                $"Sucursal de origen: {sourceBranchAccess.Error}",
                sourceBranchAccess.Code
            );

        var targetBranchAccess = await _branchAccessGuard.RequireBranchAsync(
            targetWarehouse.BranchId,
            cancellationToken
        );
        if (!targetBranchAccess.IsSuccess)
            return Result<InterBranchAccessContext>.Failure(
                $"Sucursal de destino: {targetBranchAccess.Error}",
                targetBranchAccess.Code
            );

        return Result<InterBranchAccessContext>.Success(
            new InterBranchAccessContext(
                company.UserId,
                company.TenantId,
                company.CompanyId,
                _currentBranch.BranchId,
                sourceWarehouse.Id,
                sourceWarehouse.BranchId,
                targetWarehouse.Id,
                targetWarehouse.BranchId
            )
        );
    }
}
