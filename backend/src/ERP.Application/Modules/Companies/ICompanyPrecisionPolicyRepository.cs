using ERP.Domain.Configuration.Entities;

namespace ERP.Application.Modules.Companies;

/// <summary>
/// COMPANY-PRECISION-POLICY-SSOT-01: persistencia de <see cref="CompanyPrecisionPolicy"/>.
/// Toda consulta filtra explícitamente por (TenantId, CompanyId) — fail-closed multi-tenant, sin
/// depender de un query filter global.
/// </summary>
public interface ICompanyPrecisionPolicyRepository
{
    Task<CompanyPrecisionPolicy?> FindAsync(Guid tenantId, Guid companyId, CancellationToken ct = default);

    Task AddAsync(CompanyPrecisionPolicy policy, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// True si la empresa ya tiene al menos una operación real registrada (venta autorizada,
    /// compra confirmada, gasto confirmado, movimiento de stock, pago de cliente/proveedor, o
    /// asiento contable Posted). Usado para decidir si la policy debe bloquearse antes de aplicar
    /// un update.
    /// </summary>
    Task<bool> HasRealOperationsAsync(Guid tenantId, Guid companyId, CancellationToken ct = default);
}
