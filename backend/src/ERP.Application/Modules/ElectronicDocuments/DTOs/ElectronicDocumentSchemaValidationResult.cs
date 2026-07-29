using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.DTOs;

/// <summary>
/// Resultado tipado de una validación XSD. <see cref="IsValid"/> es la única fuente de verdad
/// sobre si el XML puede avanzar a la siguiente etapa (firma electrónica) — nunca un booleano
/// suelto. Acumula todos los errores encontrados en una sola pasada, no solo el primero.
///
/// <see cref="IsValid"/> también es <c>false</c> cuando el esquema oficial requerido todavía
/// no está disponible (ver <c>IXmlSchemaProvider</c>) — en ese caso <see cref="Errors"/> lo
/// indica explícitamente en vez de dejar avanzar el flujo con una validación no realizada.
/// </summary>
public sealed record ElectronicDocumentSchemaValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    string? SchemaVersion,
    ElectronicDocumentType DocumentType
);
