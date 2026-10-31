using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Domain.Modules.ElectronicDocuments.Entities;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>Solicitud explícita de corrección y retransmisión de un comprobante rechazado.</summary>
public sealed record CorrectedRetransmissionRequest(
    Guid TenantId,
    Guid ElectronicDocumentId,
    string Reason,
    Guid IdempotencyKey,
    Guid UserId
);

/// <summary>
/// R12 Fase B — única salida de <c>Rejected</c>: regenerar el XML desde el origen confirmado (misma
/// operación, misma clave), demostrar una corrección real, reclamar el intento bajo el lock del origen
/// y transmitirlo una sola vez. Nunca la usan jobs ni <see cref="IElectronicDocumentIssuer.RetryAsync"/>:
/// solo el comando explícito del Monitor, con motivo y permiso propios.
/// </summary>
public interface IElectronicDocumentCorrectedRetransmission
{
    /// <summary>Elegibilidad calculada con la evidencia persistida (sin regenerar ni consultar al SRI).</summary>
    Task<ElectronicDocumentRetransmissionEligibility> EvaluateAsync(
        ElectronicDocument document,
        CancellationToken ct = default
    );

    Task<Result<ElectronicDocumentDto>> RetransmitCorrectedAsync(
        CorrectedRetransmissionRequest request,
        CancellationToken ct = default
    );
}
