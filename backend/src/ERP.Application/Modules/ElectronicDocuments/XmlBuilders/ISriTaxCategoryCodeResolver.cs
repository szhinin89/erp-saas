namespace ERP.Application.Modules.ElectronicDocuments.XmlBuilders;

/// <summary>
/// Resuelve el código de categoría tributaria oficial del SRI ("2" = IVA, "3" = ICE — tabla
/// de códigos de impuesto de la ficha técnica) para la etiqueta genérica que usa
/// <see cref="DTOs.ElectronicDocumentData"/> ("VAT"/"ICE"). Es el único punto del motor
/// autorizado a conocer ese mapeo — ningún XmlBuilder debe hardcodearlo inline.
///
/// Punto de extensión pendiente: a diferencia de <c>sri_vat_rates</c>/<c>sri_ice_rates</c>
/// (que sí existen y almacenan porcentajes), hoy no hay en el proyecto un catálogo de BD para
/// esta tabla de códigos de impuesto. La implementación registrada debe devolver <c>null</c>
/// si no puede resolver el código desde una fuente verificable — nunca inventar el valor
/// ("2"/"3" hardcodeados). Cuando se incorpore el catálogo correspondiente, solo esta
/// implementación cambia; el contrato y los XmlBuilders que lo consumen no se modifican.
/// </summary>
public interface ISriTaxCategoryCodeResolver
{
    string? Resolve(string taxCode);
}
