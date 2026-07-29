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
            return Result<InterBranchAccessContext>.Failure(companyAccess.Error!);

        var company = companyAccess.Value!;

        if (!_currentBranch.HasBranchContext)
            return Result<InterBranchAccessContext>.Failure(
                "Debe seleccionar una sucursal activa."
            );

        var sourceWarehouse = await _warehouseRepository.GetByIdAsync(
            company.TenantId,
            sourceWarehouseId,
            cancellationToken
        );
        if (sourceWarehouse is null)
            return Result<InterBranchAccessContext>.Failure("Bodega origen no encontrada.");

        var targetWarehouse = await _warehouseRepository.GetByIdAsync(
            company.TenantId,
            targetWarehouseId,
            cancellationToken
        );
        if (targetWarehouse is null)
            return Result<InterBranchAccessContext>.Failure("Bodega destino no encontrada.");

        if (!sourceWarehouse.IsActive)
            return Result<InterBranchAccessContext>.Failure("La bodega origen está deshabilitada.");

        if (!targetWarehouse.IsActive)
            return Result<InterBranchAccessContext>.Failure(
                "La bodega destino está deshabilitada."
            );

        if (sourceWarehouse.CompanyId != company.CompanyId)
            return Result<InterBranchAccessContext>.Failure(
                "La bodega origen no pertenece a la empresa operativa actual."
            );

        if (targetWarehouse.CompanyId != company.CompanyId)
            return Result<InterBranchAccessContext>.Failure(
                "La bodega destino no pertenece a la empresa operativa actual."
            );

        var sourceBranchAccess = await _branchAccessGuard.RequireBranchAsync(
            sourceWarehouse.BranchId,
            cancellationToken
        );
        if (!sourceBranchAccess.IsSuccess)
            return Result<InterBranchAccessContext>.Failure(
                $"Sucursal de origen: {sourceBranchAccess.Error}"
            );

        var targetBranchAccess = await _branchAccessGuard.RequireBranchAsync(
            targetWarehouse.BranchId,
            cancellationToken
        );
        if (!targetBranchAccess.IsSuccess)
            return Result<InterBranchAccessContext>.Failure(
                $"Sucursal de destino: {targetBranchAccess.Error}"
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
