using ERP.Application.Common;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>
/// ADR-036 (D-6…D-8, ZH-RETENTION-SRI-ANNULMENT-01) — único punto que mueve un comprobante por el ciclo
/// de anulación ante el SRI: Authorized→AnnulmentPending (solicitud registrada), AnnulmentPending→
/// Cancelled (ANULADO informado por el SRI, con evidencia) y AnnulmentPending→Authorized (desistida).
/// No habla con el SRI: la SOLICITUD es asistida (no existe WS para solicitarla) y el ANULADO lo obtiene
/// el llamador consultando ConsultaComprobante (<c>ISriDocumentStatusQuery</c>, ADR-036 §25).
///
/// Debe invocarse dentro de la transacción del llamador: toma el mismo lock del origen que el
/// reclamo de envío y la anulación del origen (<see cref="IElectronicDocumentSourceLifecycleGuard"/>,
/// <c>FOR UPDATE</c>) y relee el comprobante bajo ese lock. No llama SaveChanges.
/// </summary>
public interface IElectronicDocumentSriAnnulment
{
    /// <summary>Authorized→AnnulmentPending. Devuelve el comprobante (Id, clave de acceso) para la solicitud.</summary>
    Task<Result<ElectronicDocument>> BeginAsync(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        Guid sourceEntityId,
        Guid annulmentRequestId,
        Guid userId,
        CancellationToken ct = default
    );

    /// <summary>AnnulmentPending→Cancelled con la evidencia del ANULADO. Idempotente si ya está Cancelled por esa solicitud.</summary>
    Task<Result<ElectronicDocument>> ConfirmAsync(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        Guid sourceEntityId,
        Guid annulmentRequestId,
        ExternalAnnulmentEvidence evidence,
        Guid userId,
        CancellationToken ct = default
    );

    /// <summary>AnnulmentPending→Authorized: la solicitud no prosperó. Idempotente si ya volvió a Authorized.</summary>
    Task<Result<ElectronicDocument>> RevertAsync(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        Guid sourceEntityId,
        Guid annulmentRequestId,
        string reason,
        Guid userId,
        CancellationToken ct = default
    );
}

public sealed class ElectronicDocumentSriAnnulment : IElectronicDocumentSriAnnulment
{
    private readonly IElectronicDocumentRepository _repository;
    private readonly IElectronicDocumentSourceLifecycleGuardResolver _guards;
    private readonly IUnitOfWork _unitOfWork;

    public ElectronicDocumentSriAnnulment(
        IElectronicDocumentRepository repository,
        IElectronicDocumentSourceLifecycleGuardResolver guards,
        IUnitOfWork unitOfWork
    )
    {
        _repository = repository;
        _guards = guards;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ElectronicDocument>> BeginAsync(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        Guid sourceEntityId,
        Guid annulmentRequestId,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var document = await LoadUnderLockAsync(tenantId, companyId, sourceModule, sourceEntityId, ct);
        if (document is null)
            return Result<ElectronicDocument>.ValidationFailure(
                "El comprobante electrónico de la retención no existe."
            );
        if (document.CurrentState != ElectronicDocumentState.Authorized)
            return Result<ElectronicDocument>.ValidationFailure(
                document.CurrentState == ElectronicDocumentState.AnnulmentPending
                    ? ElectronicDocumentSourceCancellation.AnnulmentPendingMessage
                    : $"Solo un comprobante autorizado por el SRI requiere anulación ante el SRI (estado actual: {document.CurrentState}).",
                document.CurrentState == ElectronicDocumentState.AnnulmentPending
                    ? ApiResponseCodes.ElectronicDocuments.AnnulmentPending
                    : ApiResponseCodes.Common.ValidationError
            );

        document.MarkAnnulmentPending(annulmentRequestId, userId);
        return Result<ElectronicDocument>.Success(document);
    }

    public async Task<Result<ElectronicDocument>> ConfirmAsync(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        Guid sourceEntityId,
        Guid annulmentRequestId,
        ExternalAnnulmentEvidence evidence,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var document = await LoadUnderLockAsync(tenantId, companyId, sourceModule, sourceEntityId, ct);
        if (document is null)
            return Result<ElectronicDocument>.ValidationFailure(
                "El comprobante electrónico de la retención no existe."
            );
        if (
            document.CurrentState == ElectronicDocumentState.Cancelled
            && document.AnnulmentRequestId == annulmentRequestId
        )
            return Result<ElectronicDocument>.Success(document);

        document.ConfirmExternalAnnulment(annulmentRequestId, evidence, userId);
        return Result<ElectronicDocument>.Success(document);
    }

    public async Task<Result<ElectronicDocument>> RevertAsync(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        Guid sourceEntityId,
        Guid annulmentRequestId,
        string reason,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var document = await LoadUnderLockAsync(tenantId, companyId, sourceModule, sourceEntityId, ct);
        if (document is null)
            return Result<ElectronicDocument>.ValidationFailure(
                "El comprobante electrónico de la retención no existe."
            );
        if (
            document.CurrentState == ElectronicDocumentState.Authorized
            && document.AnnulmentRequestId is null
        )
            return Result<ElectronicDocument>.Success(document);

        document.RevertAnnulment(annulmentRequestId, reason, userId);
        return Result<ElectronicDocument>.Success(document);
    }

    private async Task<ElectronicDocument?> LoadUnderLockAsync(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        Guid sourceEntityId,
        CancellationToken ct
    )
    {
        var guard =
            _guards.Resolve(sourceModule)
            ?? throw new InvalidOperationException(
                $"Invariante violada: el módulo '{sourceModule}' no tiene IElectronicDocumentSourceLifecycleGuard registrado."
            );
        if (!_unitOfWork.HasActiveTransaction)
            throw new InvalidOperationException(
                "Invariante violada: la anulación ante el SRI debe ejecutarse dentro de una transacción."
            );

        await guard.EvaluateAsync(tenantId, companyId, sourceEntityId, lockForUpdate: true, ct);
        var document = await _repository.GetBySourceAsync(tenantId, sourceModule, sourceEntityId, ct);
        if (document is null || document.CompanyId != companyId)
            return null;
        await _repository.ReloadAsync(document, ct);
        return document;
    }
}
