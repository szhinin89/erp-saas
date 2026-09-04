using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>
/// RETENTIONS-SRI-AUTHORIZATION-WIRING-04D — entrega el <see cref="ElectronicDocumentXml"/> de un
/// documento de origen, sin que el llamador (<see cref="ElectronicDocumentIssuer"/>) necesite
/// saber si por dentro se pasó por <see cref="ElectronicDocumentData"/> (camino comercial de
/// Factura/Nota de Crédito) o por un camino propio (Retención, <see cref="RetentionElectronicDocumentXmlSupplier"/>).
///
/// Diseño aprobado en RETENTIONS-SRI-AUTHORIZATION-WIRING-DESIGN-04B, sección C/E/F/G: "Supplier"
/// se eligió para no colisionar con "Provider" (paso 1 del camino comercial) ni "Builder" (paso 2
/// del camino comercial) ni "Source" (ya usado por <see cref="ElectronicDocumentSourceReference"/>
/// para el documento de origen de negocio, no el XML).
/// </summary>
public interface IElectronicDocumentXmlSupplier
{
    ElectronicDocumentType DocumentType { get; }

    Task<Result<ElectronicDocumentXml>> BuildXmlAsync(
        ElectronicDocumentSourceReference reference,
        CancellationToken cancellationToken = default
    );
}
