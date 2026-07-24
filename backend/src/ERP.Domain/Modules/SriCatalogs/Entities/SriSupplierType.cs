namespace ERP.Domain.Modules.SriCatalogs.Entities;

/// <summary>
/// Tabla 26 (Tipo Proveedor de Reembolso) de la Ficha Técnica del SRI — Esquema Offline.
/// Catálogo global de solo lectura, mismo patrón que <c>SriVatRate</c>. Se usa directamente
/// en el registro del proveedor (<c>SupplierRoleConfig.RefundProviderTypeCode</c>), no solo
/// en el bloque XML de reembolsos — por eso el nombre de la tabla es genérico.
/// </summary>
public class SriSupplierType
{
    public string Code     { get; set; } = null!;
    public string Name     { get; set; } = null!;
    public bool   IsActive { get; set; } = true;
}
