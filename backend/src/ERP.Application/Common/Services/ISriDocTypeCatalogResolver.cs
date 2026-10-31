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

    /// <summary>True si el código existe y está activo (habilitado en el ERP), sin importar la modalidad.</summary>
    Task<bool> IsActiveDocTypeAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// PHYSICAL-FISCAL-F4/F5 — true si el régimen tributario (activo) puede emitir el tipo en forma
    /// física según <c>global.sri_tax_regime_physical_doc_type</c>. No depende de
    /// <c>is_electronic</c>: "apto electrónico" no implica "permitido en físico".
    /// </summary>
    Task<bool> IsPhysicalDocTypePermittedAsync(
        string taxRegimeCode,
        string docTypeCode,
        CancellationToken ct = default
    );
}
