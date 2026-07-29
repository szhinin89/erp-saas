using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.DTOs;

/// <summary>
/// Resultado tipado de la etapa de firma — el XML ya firmado (XAdES-BES) más los metadatos
/// que la siguiente fase (envío SOAP) necesitará. No se persiste ni se guarda a disco en esta
/// fase (eso es de la fase de almacenamiento/recepción SRI).
/// </summary>
public sealed record SignedElectronicDocumentXml(
    string SignedXml,
    string Encoding,
    string Version,
    ElectronicDocumentType DocumentType,
    string AccessKey,
    DateTime SignedAtUtc
);
