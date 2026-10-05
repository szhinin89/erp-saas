using ERP.Application.Common;
using ERP.Application.Common.Idempotency;
using ERP.Application.Modules.Caja.DTOs;
using ERP.Domain.Common;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Caja.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Caja.UseCases;

// ── Command ────────────────────────────────────────────────────────────

/// <summary>
/// TREASURY-CASH-MANUAL-MOVEMENTS-01 — <see cref="ReasonId"/> es obligatorio: este comando ahora
/// sirve EXCLUSIVAMENTE para movimientos manuales (Opening/SaleIncome/SaleRefund se rechazan en el
/// handler — esos tipos nunca deben crearse por esta vía genérica, solo por
/// <c>CashSession.Open</c>/<c>SalesInvoiceAuthorizedHandler</c>/<c>SalesReturnRefundHandler</c>).
/// </summary>
public sealed record RecordCashMovementCommand(
    Guid CashSessionId,
    string MovementType,
    Guid ReasonId,
    decimal Amount,
    string Description,
    string? ReferenceType = null,
    Guid? ReferenceId = null,
    string? ReferenceNumber = null,
    /// <summary>
    /// ZH-FINANCIAL-COMMAND-IDEMPOTENCY-01 — intención del cliente (una por movimiento que el
    /// usuario quiere registrar; estable en reintentos). Obligatorio.
    /// </summary>
    Guid ClientRequestId = default
) : IRequest<Result<CashMovementDto>>, IBranchScopedRequest;

/// <summary>
/// ZH-FINANCIAL-COMMAND-IDEMPOTENCY-01 — representación canónica V1 de la intención de un
/// movimiento manual de caja (solo datos del usuario). Cambiar su forma = V2.
/// </summary>
internal sealed record ManualCashMovementIntentV1(
    Guid CashSessionId,
    string MovementType,
    Guid ReasonId,
    decimal Amount,
    string? Description,
    string? ReferenceType,
    Guid? ReferenceId,
    string? ReferenceNumber
)
{
    public static string ComputeHash(RecordCashMovementCommand cmd) =>
        CanonicalRequestFingerprint.Compute(
            new ManualCashMovementIntentV1(
                cmd.CashSessionId,
                cmd.MovementType.Trim().ToUpperInvariant(),
                cmd.ReasonId,
                cmd.Amount,
                CanonicalRequestFingerprint.NormalizeText(cmd.Description),
                CanonicalRequestFingerprint.NormalizeText(cmd.ReferenceType)?.ToUpperInvariant(),
                cmd.ReferenceId,
                CanonicalRequestFingerprint.NormalizeText(cmd.ReferenceNumber)
            )
        );
}

// ── Validator ──────────────────────────────────────────────────────────

public sealed class RecordCashMovementValidator : AbstractValidator<RecordCashMovementCommand>
{
    public RecordCashMovementValidator()
    {
        RuleFor(x => x.ClientRequestId)
            .NotEmpty()
            .WithMessage("El identificador de idempotencia es obligatorio.");
        RuleFor(x => x.CashSessionId).NotEmpty().WithMessage("La sesión de caja es obligatoria.");
        RuleFor(x => x.MovementType)
            .NotEmpty()
            .WithMessage("El tipo de movimiento es obligatorio.");
        RuleFor(x => x.ReasonId).NotEmpty().WithMessage("El motivo es obligatorio.");
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("El monto debe ser mayor a cero.");
        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(CashMovement.DescriptionMaxLen)
            .WithMessage("La descripción es obligatoria.");
        RuleFor(x => x.ReferenceNumber).MaximumLength(CashMovement.ReferenceNumberMaxLen);
    }
}

// ── Handler ────────────────────────────────────────────────────────────

public sealed class RecordCashMovementHandler
    : IRequestHandler<RecordCashMovementCommand, Result<CashMovementDto>>
{
    /// <summary>
    /// TREASURY-CASH-MANUAL-MOVEMENTS-01 — únicos tipos que este comando genérico puede crear.
    /// Opening solo lo crea <c>CashSession.Open</c>; SaleIncome/SaleRefund solo los crean
    /// <c>SalesInvoiceAuthorizedHandler</c>/<c>SalesReturnRefundHandler</c> a partir de una venta o
    /// devolución real autorizada — permitirlos aquí abriría un hueco para inflar/desinflar el
    /// efectivo esperado sin que exista la venta/devolución detrás.
    /// </summary>
    private static readonly CashMovementType[] AllowedManualTypes =
    [
        CashMovementType.ManualIncome,
        CashMovementType.ManualExpense,
        CashMovementType.Withdrawal,
    ];

    internal const string ClientRequestConflict =
        "Ya existe un movimiento con este identificador pero con datos distintos.";

    private readonly ICashSessionRepository _repo;
    private readonly ICashMovementReasonRepository _reasonRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenant _t;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;
    private readonly IOperationalPreferencesResolver _preferences;

    public RecordCashMovementHandler(
        ICashSessionRepository repo,
        ICashMovementReasonRepository reasonRepo,
        IUnitOfWork uow,
        ICurrentTenant t,
        ICurrentBranch b,
        ICurrentUser u,
        IOperationalPreferencesResolver preferences
    )
    {
        _repo = repo;
        _reasonRepo = reasonRepo;
        _uow = uow;
        _t = t;
        _b = b;
        _u = u;
        _preferences = preferences;
    }

    public async Task<Result<CashMovementDto>> Handle(
        RecordCashMovementCommand cmd,
        CancellationToken ct
    )
    {
        var key = new ClientRequestKey(
            cmd.ClientRequestId,
            ManualCashMovementIntentV1.ComputeHash(cmd)
        );

        // Camino rápido del replay: ningún efecto, ni transacción.
        var existing = await _repo.GetMovementByClientRequestIdAsync(_t.TenantId, key.Id, ct);
        if (existing is not null)
            return Replay(existing, key);

        if (!Enum.TryParse<CashMovementType>(cmd.MovementType, true, out var movementType))
            return Result<CashMovementDto>.ValidationFailure(
                $"Tipo de movimiento '{cmd.MovementType}' no válido."
            );

        if (!AllowedManualTypes.Contains(movementType))
            return Result<CashMovementDto>.ValidationFailure(
                "Este tipo de movimiento no se puede registrar manualmente — solo Ingreso manual, Egreso manual o Retiro."
            );

        // TREASURY-CASH-MANUAL-MOVEMENTS-COMPANY-SETTING-05 — reutiliza el SSOT existente de
        // preferencias operativas (OrgSettingKeys.Cash.AllowManualInOutMovements, scope
        // Tenant+Company, default true para no romper empresas existentes) en vez de crear una
        // configuración paralela. Fail-closed: se valida ANTES de tocar el catálogo de motivos o
        // la sesión — una API directa nunca puede saltarse esto porque el frontend nunca decide.
        var preferences = await _preferences.ResolveAsync(ct);
        if (!preferences.Cash.AllowManualInOutMovements)
            return Result<CashMovementDto>.ValidationFailure(
                "Esta empresa no permite registrar movimientos manuales de caja. Contacte al administrador para habilitarlo en Preferencias operativas."
            );

        var referenceType = CashReferenceType.None;
        if (
            !string.IsNullOrWhiteSpace(cmd.ReferenceType)
            && !Enum.TryParse(cmd.ReferenceType, true, out referenceType)
        )
            return Result<CashMovementDto>.ValidationFailure(
                $"Tipo de referencia '{cmd.ReferenceType}' no válido."
            );

        await _uow.BeginTransactionAsync(ct);
        try
        {
            // Éxito = movimiento nuevo o intención ya registrada detectada bajo el lock (sin cambios:
            // el commit solo libera el lock). Cualquier rechazo revierte.
            var result = await RecordLockedAsync(cmd, key, movementType, referenceType, ct);
            if (result.IsSuccess)
                await _uow.CommitAsync(ct);
            else
                await _uow.RollbackAsync(ct);
            return result;
        }
        catch
        {
            // Última barrera (índice único): la transacción perdedora se revierte completa y, si la
            // intención ya quedó registrada por un reintento concurrente, se responde con ella.
            await _uow.RollbackAsync(ct);
            var raced = await _repo.GetMovementByClientRequestIdAsync(_t.TenantId, key.Id, ct);
            if (raced is null)
                throw;
            return Replay(raced, key);
        }
    }

    /// <summary>
    /// Sesión FOR UPDATE (serializa los movimientos de la misma caja) → la intención se vuelve a
    /// buscar bajo el lock → validaciones → un único movimiento vinculado a la intención.
    /// </summary>
    private async Task<Result<CashMovementDto>> RecordLockedAsync(
        RecordCashMovementCommand cmd,
        ClientRequestKey key,
        CashMovementType movementType,
        CashReferenceType referenceType,
        CancellationToken ct
    )
    {
        var session = await _repo.GetByIdForUpdateAsync(_t.TenantId, cmd.CashSessionId, ct);
        if (session is null || session.BranchId != _b.BranchId)
            return Result<CashMovementDto>.NotFound("Sesión de caja no encontrada.");

        var registered = await _repo.GetMovementByClientRequestIdAsync(_t.TenantId, key.Id, ct);
        if (registered is not null)
            return Replay(registered, key);

        // 02B — `caja.record` decide QUÉ puede hacer el usuario; la sesión solo la opera quien la
        // abrió (CashSession.UserId). Fail-closed, sin bypass por rol.
        if (!session.IsControlledBy(_u.UserId))
            return Result<CashMovementDto>.ValidationFailure(
                CashSessionOwnership.RejectionMessage(session)
            );

        // Fail-closed: la búsqueda ya filtra por Tenant+Company de la sesión — un motivo de otro
        // tenant o de otra empresa (aunque exista con ese Id) llega aquí como null, exactamente
        // igual que "no existe". Nunca se usa un CompanyId ambient distinto del de la sesión real.
        var reason = await _reasonRepo.GetByIdAsync(
            _t.TenantId,
            session.CompanyId,
            cmd.ReasonId,
            ct
        );
        if (reason is null)
            return Result<CashMovementDto>.ValidationFailure(
                "El motivo seleccionado no existe o no pertenece a esta empresa."
            );
        if (!reason.IsActive)
            return Result<CashMovementDto>.ValidationFailure(
                "El motivo seleccionado está inactivo."
            );
        if (reason.MovementType != movementType)
            return Result<CashMovementDto>.ValidationFailure(
                "El motivo seleccionado no corresponde a este tipo de movimiento."
            );

        var movement = session.RecordMovement(
            movementType,
            cmd.Amount,
            cmd.Description,
            _u.UserId,
            referenceType,
            cmd.ReferenceId,
            cmd.ReferenceNumber,
            reason.Id,
            reason.Name
        );
        movement.BindClientRequest(key);

        await _repo.SaveChangesAsync(ct);

        return Result<CashMovementDto>.Success(ToDto(movement));
    }

    /// <summary>Mismo request → el movimiento ya registrado; request distinto → Conflict.</summary>
    private Result<CashMovementDto> Replay(CashMovement existing, ClientRequestKey key) =>
        key.Matches(existing.RequestPayloadHash)
            ? Result<CashMovementDto>.Success(ToDto(existing))
            : Result<CashMovementDto>.Conflict(ClientRequestConflict);

    private CashMovementDto ToDto(CashMovement movement) =>
        new(
            movement.Id,
            movement.MovementType.ToString(),
            movement.Amount,
            movement.Description,
            movement.CreatedAt,
            movement.CreatedBy,
            _u.FullName,
            movement.ReferenceType.ToString(),
            movement.ReferenceId,
            movement.ReferenceNumber,
            movement.ReasonId,
            movement.ReasonName
        );
}
