using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;

namespace ERP.Infrastructure.Services.ElectronicDocuments;

/// <summary>
/// Resuelve el código de categoría tributaria SRI ("tipo de impuesto" — Tabla de Códigos de
/// Impuesto de la ficha técnica de comprobantes electrónicos) para las tres categorías que
/// produce hoy el motor de ElectronicDocuments: "VAT" (IVA → código "2"), "ICE" (ICE → código
/// "3") e "IRBPNR" (IRBPNR → código "5", ELECTRONIC-DOCUMENTS-IRBPNR-CATEGORY-01, cerrado
/// 2026-08-29 — ver ADR-032 §9). Ver
/// <see cref="Sales.Services.SalesInvoiceElectronicDocumentDataProvider"/>, única fuente que
/// emite esas etiquetas.
///
/// A diferencia de <c>sri_vat_rates</c>/<c>sri_ice_rates</c>/<c>sri_irbpnr_rates</c> (catálogo de
/// BD porque las tarifas varían con el tiempo y por porcentaje), esta tabla es una constante
/// regulatoria fija del propio esquema XML del SRI: nunca cambia por tenant ni con el tiempo
/// dentro de la versión de esquema "1.1.0" — es parte de la especificación del formato, igual
/// que el nombre del elemento <c>infoTributaria</c> o la longitud de 49 dígitos de la clave de
/// acceso (ambos ya hardcodeados como constantes en <c>InvoiceXmlBuilder</c>). Crear una tabla de
/// BD para tres valores inmutables de especificación sería sobre-ingeniería, no una fuente de
/// verdad adicional real.
///
/// Nunca inventa un valor para un <c>taxCode</c> no reconocido — devuelve <c>null</c>, igual que
/// la implementación anterior (<c>PendingCatalogSriTaxCategoryCodeResolver</c>, reemplazada por
/// esta clase en la Fase 9).
/// </summary>
public sealed class SriTaxCategoryCodeResolver : ISriTaxCategoryCodeResolver
{
    private static readonly IReadOnlyDictionary<string, string> CodeByTaxCode = new Dictionary<
        string,
        string
    >(StringComparer.Ordinal)
    {
        ["VAT"] = "2",
        ["ICE"] = "3",
        ["IRBPNR"] = "5",
    };

    public string? Resolve(string taxCode) =>
        CodeByTaxCode.TryGetValue(taxCode, out var code) ? code : null;
}
