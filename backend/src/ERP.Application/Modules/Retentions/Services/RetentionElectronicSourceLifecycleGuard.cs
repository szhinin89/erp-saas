using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Modules.Retentions;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Interfaces;

namespace ERP.Application.Modules.Retentions.Services;

/// <summary>
/// ADR-036 (D-1, I-1) — una retención permite procesamiento electrónico ÚNICAMENTE si está
/// <see cref="RetentionStatus.Issued"/>. Draft (sin número) y Cancelled (terminal) nunca registran,
/// generan XML, firman, envían, reenvían ni reactivan su comprobante. Lee el estado actual de la BD
/// filtrado por tenant y empresa (fail-closed: inexistente en ese alcance = no procesable).
/// </summary>
public sealed class RetentionElectronicSourceLifecycleGuard : IElectronicDocumentSourceLifecycleGuard
{
    private readonly IRetentionDocumentRepository _retentions;

    public RetentionElectronicSourceLifecycleGuard(IRetentionDocumentRepository retentions) =>
        _retentions = retentions;

    public string SourceModule => RetentionElectronicDocumentSource.SourceModule;

    public async Task<ElectronicDocumentSourceLifecycle> EvaluateAsync(
        Guid tenantId,
        Guid companyId,
        Guid sourceEntityId,
        bool lockForUpdate,
        CancellationToken ct = default
    )
    {
        var status = await _retentions.GetCurrentStatusAsync(
            tenantId,
            companyId,
            sourceEntityId,
            lockForUpdate,
            ct
        );

        return status switch
        {
            RetentionStatus.Issued => ElectronicDocumentSourceLifecycle.Allowed,
            null => ElectronicDocumentSourceLifecycle.Denied("La retención no existe."),
            RetentionStatus.Cancelled => ElectronicDocumentSourceLifecycle.Denied(
                "La retención está anulada: su comprobante electrónico no puede procesarse."
            ),
            _ => ElectronicDocumentSourceLifecycle.Denied(
                $"La retención debe estar emitida para procesar su comprobante electrónico (estado actual: {status})."
            ),
        };
    }
}
