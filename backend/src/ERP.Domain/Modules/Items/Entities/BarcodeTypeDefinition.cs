namespace ERP.Domain.Modules.Items.Entities;

/// <summary>
/// Catálogo global de solo lectura de tipos de código de barras (EAN-13, EAN-8, QR, ...).
/// Reemplaza el enum cerrado <c>BarcodeType</c>. Sigue exactamente el patrón de
/// <see cref="ERP.Domain.Modules.SriCatalogs.Entities.SriVatRate"/> — schema <c>global</c>,
/// PK=Code, seed vía HasData, sin CRUD.
/// </summary>
public class BarcodeTypeDefinition
{
    public string Code     { get; set; } = null!;
    public string Name     { get; set; } = null!;
    public bool   IsActive { get; set; } = true;
}
