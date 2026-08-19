namespace ERP.Domain.Modules.Sales.Interfaces;

/// <summary>
/// CONFIG-FOUNDATION-P1-04: valores por defecto ya resueltos para una nueva factura de venta.
/// <see cref="DefaultWarehouseSource"/> es uno de "BranchSetting" | "BranchMainWarehouse" | "None"
/// (ver Fase 3/4 de docs/architecture/configuration-engine-target-architecture.md — el valor
/// "CashRegister", de mayor precedencia, se resuelve en frontend a partir de la sesión de caja
/// activa, fuera de este resolver).
/// </summary>
public sealed record InvoiceDefaultsResult(
    string? DefaultDocTypeCode,
    string? DefaultSriPaymentMethodCode,
    Guid? DefaultPaymentTermId,
    Guid? DefaultEmissionPointId,
    Guid? DefaultWarehouseId,
    string DefaultWarehouseSource,
    bool RequiresManualWarehouseSelection,
    IReadOnlyList<string> ConfigurationWarnings
);

/// <summary>
/// Único punto de resolución de defaults de factura de venta (doc type, forma de pago SRI,
/// condición de pago, punto de emisión, bodega). Ningún handler operativo debe leer
/// IOrgSettingsRepository/OrgSettingKeys.Invoice.* directamente — deben depender de este resolver.
///
/// Precedencia de bodega (CONFIG-FOUNDATION-P0-01): Branch OrgSetting (validado: debe existir,
/// estar activa y pertenecer a la sucursal) → Warehouse.IsMain de la sucursal → null +
/// RequiresManualWarehouseSelection. Fail-closed: un valor de bodega corrupto o cruzado nunca cae
/// en silencio al siguiente nivel — ver ConfigurationWarnings.
/// </summary>
public interface IInvoiceDefaultsResolver
{
    Task<InvoiceDefaultsResult> GetAsync(
        Guid tenantId,
        Guid companyId,
        Guid? branchId,
        CancellationToken cancellationToken = default
    );
}
