using ERP.Domain.Modules.Finance.Entities;

namespace ERP.Domain.Modules.Finance.Interfaces;

/// <summary>
/// Contrato de persistencia de <see cref="CompanyFinancialDestination"/> — diseño P0-02 §6.4, Fase 2.
/// El bloqueo <c>FOR SHARE</c> real usado por <c>RegisterRefund</c> (§6.4quater) se agrega en la
/// Fase 8, junto con el caso de uso que lo consume — fuera del alcance de persistencia base.
/// </summary>
public interface ICompanyFinancialDestinationRepository
{
    Task<CompanyFinancialDestination?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    );

    /// <summary>
    /// P0-02 Fase 8 — bloqueo real <c>SELECT ... FOR SHARE</c> sobre la fila (§6.4quater paso 3),
    /// adquirido dentro de la transacción ambiente ya abierta por <c>RegisterSupplierCreditRefundUseCases</c>
    /// (después de Lock B). Se libera automáticamente al COMMIT/ROLLBACK — nunca abre transacción propia.
    /// </summary>
    Task<CompanyFinancialDestination?> GetByIdForShareAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    );

    Task AddAsync(CompanyFinancialDestination destination, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// P0-02 Fase 13 Remediación 01 — listado para el selector de destino financiero (Finance
    /// frontend: reembolso de crédito de proveedor) y para la administración limitada de
    /// <c>CompanyFinancialDestination</c> (§6.4ter). Extensión puramente aditiva — no reemplaza ni
    /// modifica los 4 casos de uso ya congelados en Fase 4/11.
    /// </summary>
    Task<IReadOnlyList<CompanyFinancialDestination>> GetListAsync(
        Guid tenantId,
        bool? isActive,
        CancellationToken ct = default
    );
}
