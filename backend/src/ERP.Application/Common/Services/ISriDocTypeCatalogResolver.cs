namespace ERP.Application.Common.Services;

/// <summary>
/// Valida códigos de tipo de comprobante SRI contra el catálogo <c>sri_doc_types</c> —
/// única fuente de verdad. Ningún módulo debe asumir que un código ("01", "04", ...) sigue
/// vigente sin consultarlo aquí primero.
/// </summary>
public interface ISriDocTypeCatalogResolver
{
    /// <summary>True si el código existe, está activo y habilitado para emisión electrónica.</summary>
    Task<bool> IsActiveElectronicDocTypeAsync(string code, CancellationToken ct = default);
}
