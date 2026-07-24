using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.DTOs;

/// <summary>
/// Resultado tipado de un <c>IElectronicDocumentXmlBuilder</c> — el XML ya construido más los
/// metadatos que las fases siguientes (validación XSD, firma, envío) necesitarán sin tener que
/// volver a parsear la cadena. No se persiste ni se guarda a disco en esta fase.
/// </summary>
public sealed record ElectronicDocumentXml(
    string Xml,
    string Encoding,
    string Version,
    ElectronicDocumentType DocumentType,
    string AccessKey,
    DateTime GeneratedAtUtc);
