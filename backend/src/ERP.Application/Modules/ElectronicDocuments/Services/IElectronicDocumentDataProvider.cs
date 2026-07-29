using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>
/// Referencia mínima que el motor de ElectronicDocuments pasa a un proveedor para que
/// obtenga los datos del documento de origen. No lleva SourceModule: el proveedor ya está
/// dedicado a un único <see cref="ElectronicDocumentType"/> (ver <see cref="IElectronicDocumentDataProvider.DocumentType"/>),
/// así que resolver "cuál módulo" ya quedó decidido antes de llegar aquí.
/// </summary>
public sealed record ElectronicDocumentSourceReference(
    Guid TenantId,
    Guid CompanyId,
    Guid SourceEntityId
);

/// <summary>
/// Contrato que cada módulo de negocio (Sales, Purchases, Inventory, ...) implementa para
/// exponer sus documentos como <see cref="ElectronicDocumentData"/>. ElectronicDocuments
/// depende únicamente de esta interfaz — nunca de un repositorio, entidad o DbContext de
/// otro módulo. La dependencia va siempre en un sentido: el módulo de negocio implementa
/// este contrato, ElectronicDocuments lo consume.
///
/// El contrato es deliberadamente genérico: no menciona Factura, Nota de Crédito, Sales
/// ni Purchases — cualquier tipo de comprobante se resuelve mediante <see cref="DocumentType"/>.
/// </summary>
public interface IElectronicDocumentDataProvider
{
    /// <summary>Tipo de documento que este proveedor sabe resolver. Un proveedor por tipo.</summary>
    ElectronicDocumentType DocumentType { get; }

    /// <summary>
    /// Devuelve el modelo común del documento de origen. Falla explícitamente (sin lanzar
    /// excepción) en dos casos distintos: el documento de origen no existe
    /// (<see cref="Result{T}.NotFound"/>), o existe pero le falta información obligatoria
    /// para emisión electrónica (<see cref="Result{T}.ValidationFailure"/>) — el proveedor
    /// debe validar completitud (empresa, establecimiento, punto de emisión, secuencial,
    /// impuestos, detalles, formas de pago, ...) antes de construir el modelo.
    /// </summary>
    Task<Result<ElectronicDocumentData>> GetDataAsync(
        ElectronicDocumentSourceReference reference,
        CancellationToken ct = default
    );
}
