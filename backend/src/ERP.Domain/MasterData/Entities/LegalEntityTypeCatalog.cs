namespace ERP.Domain.MasterData.Entities;

/// <summary>
/// Catálogo global de tipos de entidad legal.
///
/// Define la naturaleza jurídica del tercero dentro del ERP:
/// 1 = Persona Natural
/// 2 = Sociedad Privada
/// 3 = Institución Pública
///
/// Este catálogo es persistido en base de datos y es la fuente
/// consultable para UI, validaciones fiscales y reglas de negocio.
///
/// BusinessPartner almacena únicamente LegalEntityTypeCode como FK
/// hacia este catálogo.
///
/// NO representa:
/// - Tipo de proveedor SRI (SupplierType)
/// - Rol del tercero (Supplier, Customer, etc.)
/// - Clasificación comercial
/// </summary>
public class LegalEntityTypeCatalog
{
    public int Code { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>
    /// Clasificación tributaria interna/SRI asociada.
    /// Ejemplos:
    /// NATURAL
    /// PRIVATE
    /// PUBLIC
    /// </summary>
    public string SriTaxCategory { get; set; }
    public bool IsActive { get; set; }
}
