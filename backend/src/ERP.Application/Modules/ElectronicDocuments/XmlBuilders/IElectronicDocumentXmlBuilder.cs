using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.XmlBuilders;

/// <summary>
/// Construye el XML de un comprobante electrónico a partir, exclusivamente, del modelo común
/// <see cref="ElectronicDocumentData"/>. No conoce SalesInvoice, PurchaseInvoice, EF Core,
/// repositorios ni ningún módulo de negocio — solo el contrato de la Fase 2/3.
///
/// Responsabilidad estrictamente acotada: construir y validar estructuralmente el XML.
/// No valida contra XSD, no firma, no envía al SRI, no guarda archivos.
/// </summary>
public interface IElectronicDocumentXmlBuilder
{
    /// <summary>Tipo de documento que este builder sabe construir. Un builder por tipo.</summary>
    ElectronicDocumentType DocumentType { get; }

    /// <summary>
    /// Construye el XML. Falla con <see cref="Result{T}.ValidationFailure"/> (nunca con una
    /// excepción) si el modelo común carece de información obligatoria o el XML resultante
    /// no queda bien formado.
    /// </summary>
    Result<ElectronicDocumentXml> Build(ElectronicDocumentData data);
}
