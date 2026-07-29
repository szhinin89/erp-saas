using System.Xml.Schema;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.SchemaValidation;

/// <summary>
/// Resuelve el <see cref="XmlSchemaSet"/> oficial del SRI para un tipo de comprobante y una
/// versión de esquema (p.ej. Factura "1.1.0"). No asume rutas fijas ni un único comprobante:
/// la combinación (<paramref name="documentType"/>, <paramref name="schemaVersion"/> — ver
/// <see cref="GetSchemaSetAsync"/>) es la clave de resolución, lo que permite convivir con
/// varias versiones del mismo comprobante (p.ej. Factura 1.1.0 y una futura 1.2.0) sin cambiar
/// el contrato.
///
/// Devuelve <c>null</c> — nunca lanza una excepción — cuando el esquema todavía no fue
/// incorporado como recurso. Es responsabilidad del validador decidir qué significa esa
/// ausencia para el flujo (ver <see cref="IElectronicDocumentSchemaValidator"/>).
/// </summary>
public interface IXmlSchemaProvider
{
    Task<XmlSchemaSet?> GetSchemaSetAsync(
        ElectronicDocumentType documentType,
        string schemaVersion,
        CancellationToken ct = default
    );
}
