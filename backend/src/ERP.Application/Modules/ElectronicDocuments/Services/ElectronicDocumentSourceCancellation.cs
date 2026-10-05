using ERP.Application.Common;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>Qué ocurrió con el documento electrónico al preparar la anulación de su origen.</summary>
public enum ElectronicDocumentSourceCancellationOutcome
{
    /// <summary>El origen nunca tuvo documento electrónico.</summary>
    NoElectronicDocument = 1,

    /// <summary>El documento nunca tuvo intento externo y quedó Discarded (en staging, sin SaveChanges).</summary>
    Discarded = 2,

    /// <summary>El documento ya no tiene nada vigente ante el SRI (Rejected/Discarded/Cancelled).</summary>
    NothingInForce = 3,
}

/// <summary>
/// ADR-036 (D-2, D-5, D-6) — única decisión de "¿puede anularse localmente un documento de origen
/// dado el estado de su comprobante electrónico?". La semántica de los estados electrónicos vive
/// aquí, en ElectronicDocuments; el módulo dueño (Retentions) solo pregunta.
///
/// Debe invocarse DENTRO de la transacción de la anulación del origen: toma el mismo lock del guard
/// (<c>FOR UPDATE</c> sobre la fila del origen) que <see cref="ElectronicDocumentIssuer"/> toma para
/// reclamar el envío, y relee el documento electrónico bajo ese lock. Así, si la anulación gana
/// primero, el emisor ya no puede reclamar el envío; si el emisor reclamó primero, la anulación ve
/// Dispatching y se bloquea.
///
/// No llama SaveChanges: el descarte queda en staging para persistirse en el mismo SaveChanges de
/// la anulación del origen (atómico con la retención, la CxP y los asientos).
/// </summary>
public interface IElectronicDocumentSourceCancellation
{
    Task<Result<ElectronicDocumentSourceCancellationOutcome>> PrepareAsync(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        Guid sourceEntityId,
        string reason,
        Guid userId,
        CancellationToken ct = default
    );
}

public sealed class ElectronicDocumentSourceCancellation : IElectronicDocumentSourceCancellation
{
    public const string InProcessMessage =
        "La retención ya está en proceso electrónico. Primero debe resolverse su estado ante el SRI.";

    public const string AnnulmentPendingMessage =
        "La retención tiene una anulación en trámite ante el SRI. El documento se anulará automáticamente cuando el SRI confirme ANULADO.";

    public const string AuthorizedMessage =
        "La retención ya fue autorizada por el SRI. Para anular el documento se requiere el proceso de anulación electrónica ante el SRI.";

    private readonly IElectronicDocumentRepository _repository;
    private readonly IElectronicDocumentSourceLifecycleGuardResolver _guards;
    private readonly IUnitOfWork _unitOfWork;

    public ElectronicDocumentSourceCancellation(
        IElectronicDocumentRepository repository,
        IElectronicDocumentSourceLifecycleGuardResolver guards,
        IUnitOfWork unitOfWork
    )
    {
        _repository = repository;
        _guards = guards;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ElectronicDocumentSourceCancellationOutcome>> PrepareAsync(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        Guid sourceEntityId,
        string reason,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var guard =
            _guards.Resolve(sourceModule)
            ?? throw new InvalidOperationException(
                $"El módulo '{sourceModule}' no tiene IElectronicDocumentSourceLifecycleGuard registrado."
            );
        if (!_unitOfWork.HasActiveTransaction)
            throw new InvalidOperationException(
                "La anulación de un origen con documento electrónico debe ejecutarse dentro de una transacción."
            );

        // Lock del origen (el mismo del reclamo de envío) ANTES de leer el documento electrónico.
        await guard.EvaluateAsync(tenantId, companyId, sourceEntityId, lockForUpdate: true, ct);

        var document = await _repository.GetBySourceAsync(
            tenantId,
            sourceModule,
            sourceEntityId,
            ct
        );
        if (document is null)
            return Result<ElectronicDocumentSourceCancellationOutcome>.Success(
                ElectronicDocumentSourceCancellationOutcome.NoElectronicDocument
            );

        // Una instancia ya trackeada puede ser previa al lock: se relee bajo el lock.
        await _repository.ReloadAsync(document, ct);

        if (document.CanBeDiscarded)
        {
            document.MarkDiscarded(reason, userId);
            return Result<ElectronicDocumentSourceCancellationOutcome>.Success(
                ElectronicDocumentSourceCancellationOutcome.Discarded
            );
        }

        return document.CurrentState switch
        {
            ElectronicDocumentState.Rejected
            or ElectronicDocumentState.Discarded
            or ElectronicDocumentState.Cancelled =>
                Result<ElectronicDocumentSourceCancellationOutcome>.Success(
                    ElectronicDocumentSourceCancellationOutcome.NothingInForce
                ),
            ElectronicDocumentState.AnnulmentPending =>
                Result<ElectronicDocumentSourceCancellationOutcome>.ValidationFailure(
                    AnnulmentPendingMessage,
                    ApiResponseCodes.ElectronicDocuments.AnnulmentPending
                ),
            ElectronicDocumentState.Authorized =>
                Result<ElectronicDocumentSourceCancellationOutcome>.ValidationFailure(
                    AuthorizedMessage,
                    ApiResponseCodes.ElectronicDocuments.SourceCancellationRequiresSriAnnulment
                ),
            // Signed (histórico: ambiguo), Dispatching, Sent, Received, XmlGenerated o un DeadLetter
            // que venía de cualquiera de ellos: el comprobante salió o pudo salir al SRI.
            _ => Result<ElectronicDocumentSourceCancellationOutcome>.ValidationFailure(
                InProcessMessage,
                ApiResponseCodes.ElectronicDocuments.SourceCancellationInProcess
            ),
        };
    }
}
