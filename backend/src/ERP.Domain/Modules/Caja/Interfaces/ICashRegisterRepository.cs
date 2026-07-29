using ERP.Domain.Modules.Caja.Entities;

namespace ERP.Domain.Modules.Caja.Interfaces;

public interface ICashRegisterRepository
{
    Task<CashRegister?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CashRegister>> GetByBranchAsync(
        Guid tenantId,
        Guid branchId,
        bool? activeFilter,
        CancellationToken ct = default
    );

    /// <summary>Lista todas las cajas de la empresa (todas las sucursales) con Branch/EmissionPoint cargados — para la administración de Cajas.</summary>
    Task<IReadOnlyList<CashRegister>> GetAllByCompanyAsync(
        Guid tenantId,
        Guid companyId,
        bool? activeFilter,
        string? search,
        CancellationToken ct = default
    );
    Task<bool> ExistsByCodeAsync(
        Guid tenantId,
        Guid branchId,
        string code,
        Guid? exceptId = null,
        CancellationToken ct = default
    );
    Task AddAsync(CashRegister cashRegister, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
