using ERP.Application.Common;
using ERP.Domain.MasterData.Entities;

namespace ERP.Application.MasterData.Services;

/// <summary>
/// Fuente única de resolución de PaymentTerm para Compras/Gastos/Ventas — ADR-033, Fases 3b/3c.
///
/// Cadena de resolución (idéntica para compra y venta, solo cambia la fuente del default):
/// PaymentTermId explícito del documento (si viene, validado activo) → default company-scoped
/// del tercero (CompanyBpPurchaseSettings para proveedor, CompanyBpSalesSettings para
/// cliente, ambos validados activos) → exigir selección explícita. Nunca "primer registro" del
/// catálogo, nunca inferencia por duración numérica, nunca una condición inactiva,
/// nunca fallback silencioso a SupplierRoleConfig.PaymentTermId ni a un default genérico de
/// empresa.
/// </summary>
public interface IPaymentTermDefaultResolver
{
    Task<Result<PaymentTerm>> ResolveForPurchaseAsync(
        Guid supplierId,
        Guid? explicitPaymentTermId,
        CancellationToken ct = default
    );

    Task<Result<PaymentTerm>> ResolveForSaleAsync(
        Guid customerId,
        Guid? explicitPaymentTermId,
        CancellationToken ct = default
    );
}
