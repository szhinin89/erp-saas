using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.XmlBuilders;

/// <summary>
/// RETENTIONS-SRI-XML-MAPPER-03B — construye el XML del Comprobante de Retención SRI a partir,
/// exclusivamente, de <see cref="RetentionElectronicDocumentData"/>.
///
/// Deliberadamente NO implementa <see cref="IElectronicDocumentXmlBuilder"/> (esa interfaz está
/// fija a <c>ElectronicDocumentData</c>, la forma comercial de Factura/Nota de Crédito — no la
/// forma de <c>RetentionElectronicDocumentData</c>, que no tiene detalle comercial ni totales de
/// venta). Mismo criterio de fork ya adoptado en RETENTIONS-ELECTRONIC-DOCUMENT-MODEL-03A para
/// <see cref="Retentions.Services.IRetentionElectronicDocumentDataProvider"/>: contrato propio,
/// sin tocar el motor genérico de ElectronicDocuments (<see cref="IElectronicDocumentXmlBuilderResolver"/>)
/// en esta fase — esa decisión de wiring final queda pendiente y deliberadamente no se activa aquí.
/// </summary>
public interface IRetentionXmlBuilder
{
    ElectronicDocumentType DocumentType { get; }

    Result<ElectronicDocumentXml> Build(RetentionElectronicDocumentData data);
}
