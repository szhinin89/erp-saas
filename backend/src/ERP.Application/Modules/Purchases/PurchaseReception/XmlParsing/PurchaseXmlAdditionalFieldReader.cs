using System.Xml.Linq;

namespace ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;

/// <summary>
/// PURCHASE-XML-LINE-ADDITIONAL-FIELDS-01 — snapshot fiel de un <c>&lt;detAdicional&gt;</c> del XML
/// (SRI factura, <c>detalle/detallesAdicionales</c>). Nombre y valor tal como los declaró el
/// proveedor, en el mismo orden en que aparecen — nunca se normaliza, traduce ni interpreta aquí.
/// </summary>
public readonly record struct XmlAdditionalField(string Name, string Value, int Position);

/// <summary>
/// Único lector de <c>detalle/detallesAdicionales/detAdicional</c> — usado tanto por
/// <see cref="PurchaseXmlDraftParser"/> (ingesta que persiste el snapshot documental de línea) como
/// por <see cref="PurchaseReceptionXmlViewExtractor"/> (vista de solo lectura de Recepción
/// Electrónica). Antes de esta clase, ambos parsers tenían su propia copia de esta lógica —
/// PURCHASE-XML-LINE-ADDITIONAL-FIELDS-01 la extrae a un único punto para no duplicarla otra vez.
/// El esquema SRI define <c>nombre</c>/<c>valor</c> como atributos del elemento
/// <c>&lt;detAdicional&gt;</c> (maxLength 300 cada uno) — algunos emisores no conformes los declaran
/// como elementos hijos en su lugar, así que se intenta primero el elemento y se cae al atributo.
/// Nunca descarta un <c>detAdicional</c> repetido: si el XML trae el mismo nombre dos veces, ambas
/// entradas se conservan (una fila por cada nodo, no un diccionario).
/// </summary>
internal static class PurchaseXmlAdditionalFieldReader
{
    public static IReadOnlyList<XmlAdditionalField> Read(XElement? detallesAdicionales) =>
        detallesAdicionales
            ?.Elements("detAdicional")
            .Select(
                (d, index) =>
                    new XmlAdditionalField(
                        Name: OptionalText(d, "nombre") ?? d.Attribute("nombre")?.Value ?? string.Empty,
                        Value: OptionalText(d, "valor") ?? d.Attribute("valor")?.Value ?? string.Empty,
                        Position: index
                    )
            )
            .Where(f => !string.IsNullOrWhiteSpace(f.Name) || !string.IsNullOrWhiteSpace(f.Value))
            .ToList()
        ?? [];

    private static string? OptionalText(XElement? parent, string name) =>
        parent?.Element(name)?.Value is { Length: > 0 } value ? value : null;
}
