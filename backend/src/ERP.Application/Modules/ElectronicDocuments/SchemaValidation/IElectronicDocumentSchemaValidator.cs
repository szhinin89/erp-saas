using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.SchemaValidation;

/// <summary>
/// Valida un <see cref="ElectronicDocumentXml"/> contra el esquema XSD oficial del SRI que le
/// corresponde. No modifica el XML, no lo firma, no corrige errores — solo informa si es
/// estructuralmente válido. El contrato es genérico: no menciona Factura ni ningún tipo
/// concreto, cualquier comprobante se resuelve mediante <see cref="DocumentType"/>.
/// </summary>
public interface IElectronicDocumentSchemaValidator
{
    /// <summary>Tipo de documento que este validador sabe validar. Un validador por tipo.</summary>
    ElectronicDocumentType DocumentType { get; }

    Task<ElectronicDocumentSchemaValidationResult> ValidateAsync(
        ElectronicDocumentXml xml, CancellationToken ct = default);
}
