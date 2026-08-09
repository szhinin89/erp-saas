namespace ERP.Domain.Modules.SriCatalogs.Constants;

/// <summary>
/// Códigos oficiales SRI de tipo de documento (tabla <c>global.sri_doc_type</c> / Ficha Técnica)
/// usados como identidad fija en lógica interna (defaults, secuencias documentales, emisión) —
/// no configurables por tenant/empresa. El catálogo completo administrable sigue viviendo en
/// <see cref="Entities.SriDocType"/> (expuesto vía <c>GET .../sri-doc-types</c> para pantallas de
/// configuración); esta clase solo nombra los códigos que hoy se necesitan como literal en código,
/// para no repetir el string mágico de forma independiente en cada módulo (Sales/Purchases/
/// Configuration). Solo incluye los códigos con uso real como literal — no agregar códigos sin un
/// consumidor concreto.
/// </summary>
public static class SriDocumentTypeCodes
{
    public const string Invoice = "01";
    public const string CreditNote = "04";
    public const string Withholding = "07";
}
