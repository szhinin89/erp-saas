using ERP.Application.Common;
using ERP.Domain.MasterData.Entities;

namespace ERP.Application.MasterData.Services;

/// <summary>
/// Fuente única de resolución de PaymentTerm para Compras/Gastos — ADR-033, Fase 3b.
///
/// Cadena de resolución: PaymentTermId explícito del documento (si viene, validado activo) →
/// default company-scoped del proveedor (CompanyBpPurchaseSettings, Fase 3a, validado activo) →
/// exigir selección explícita. Nunca "primer registro" del catálogo, nunca inferencia por días,
/// nunca una condición inactiva, nunca fallback silencioso a SupplierRoleConfig.PaymentTermId
/// (tenant-wide) — esa propiedad se mantiene sin cambios pero deja de ser fuente operativa.
///
/// Ventas queda fuera de esta fase (ResolveForSaleAsync se agrega en Fase 3c cuando exista
/// consumidor real).
/// </summary>
public interface IPaymentTermDefaultResolver
{
    Task<Result<PaymentTerm>> ResolveForPurchaseAsync(
        Guid supplierId,
        Guid? explicitPaymentTermId,
        CancellationToken ct = default
    );
}
