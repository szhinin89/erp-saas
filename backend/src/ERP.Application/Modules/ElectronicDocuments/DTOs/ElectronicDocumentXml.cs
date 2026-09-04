using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.DTOs;

/// <summary>
/// Resultado tipado de un <c>IElectronicDocumentXmlBuilder</c>/<c>IElectronicDocumentXmlSupplier</c>
/// (RETENTIONS-SRI-AUTHORIZATION-WIRING-04D) — el XML ya construido más los metadatos que las
/// fases siguientes (validación XSD, firma, envío) necesitarán sin tener que volver a parsear la
/// cadena. No se persiste ni se guarda a disco en esta fase.
///
/// <see cref="Environment"/> ("1"=Pruebas, "2"=Producción — Ficha Técnica SRI Tabla 4) se agregó
/// en 04D: antes, <c>ElectronicDocumentIssuer</c> lo leía de <c>ElectronicDocumentData.Emission.Environment</c>
/// (el modelo comercial intermedio), pero con <c>IElectronicDocumentXmlSupplier</c> ese modelo
/// deja de estar garantizado (el supplier de Retención nunca lo produce) — todo productor de
/// <c>ElectronicDocumentXml</c> ya conoce este dato en el momento de construirlo, así que no es
/// un dato nuevo a obtener, solo un campo nuevo a poblar.
/// </summary>
public sealed record ElectronicDocumentXml(
    string Xml,
    string Encoding,
    string Version,
    ElectronicDocumentType DocumentType,
    string Environment,
    string AccessKey,
    DateTime GeneratedAtUtc
);
