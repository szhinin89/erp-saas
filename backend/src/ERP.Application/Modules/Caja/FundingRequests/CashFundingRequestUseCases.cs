using System.Text.Json;
using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Branches;
using ERP.Application.Modules.Payables.Services;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Caja.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Caja.FundingRequests;

// ── Request HTTP ─────────────────────────────────────────────────────────

/// <summary>
/// ZH-CASH-FUNDING-REQUEST-API-02E-D — cuerpo de <c>POST /api/v1/cash-funding-requests</c>: exactamente
/// el mismo pago que <see cref="RegisterSupplierPaymentRequest"/> + la clave de idempotencia.
/// </summary>
public sealed record CreateCashFundingRequestRequest(
    Guid SupplierId,
    DateOnly PaymentDate,
    decimal TotalAmount,
    string? ReceiptNumber,
    IReadOnlyList<SupplierPaymentMethodLineRequest> MethodLines,
    IReadOnlyList<SupplierPaymentApplicationLineRequest>? ApplicationLines,
    IReadOnlyList<SupplierPaymentAllocationLineRequest>? Allocations,
    Guid ClientRequestId,
    bool ConfirmUnappliedAmount = false
);

// ── Commands ─────────────────────────────────────────────────────────────

/// <summary>
/// Solicitar efectivo de una caja operada por OTRO usuario para un pago a proveedor.
/// <see cref="Payment"/> es exactamente el mismo pago que se registraría directo. Sin ningún efecto
/// financiero: se guarda como snapshot V1 y se ejecuta solo al <see cref="FulfillCashFundingRequestCommand"/>.
/// </summary>
public sealed record CreateCashFundingRequestCommand(RegisterSupplierPaymentCommand Payment, Guid ClientRequestId)
    : IRequest<Result<CashFundingRequestDto>>,
        IBranchScopedRequest;

/// <summary>El cajero que controla la sesión entrega el efectivo: ejecuta el pago en una sola transacción.</summary>
public sealed record FulfillCashFundingRequestCommand(Guid Id)
    : IRequest<Result<CashFundingRequestDto>>,
        IBranchScopedRequest;

/// <summary>El cajero que controla la sesión rechaza la solicitud (motivo obligatorio). Sin efectos financieros.</summary>
public sealed record RejectCashFundingRequestCommand(Guid Id, string Reason)
    : IRequest<Result<CashFundingRequestDto>>,
        IBranchScopedRequest;

/// <summary>El solicitante cancela su solicitud (motivo obligatorio). Sin efectos financieros.</summary>
public sealed record CancelCashFundingRequestCommand(Guid Id, string Reason)
    : IRequest<Result<CashFundingRequestDto>>,
        ICompanyScopedRequest;

// ── Validators ───────────────────────────────────────────────────────────

public sealed class CreateCashFundingRequestValidator : AbstractValidator<CreateCashFundingRequestCommand>
{
    public CreateCashFundingRequestValidator()
    {
        RuleFor(x => x.ClientRequestId).NotEmpty().WithMessage("El identificador de idempotencia es obligatorio.");
        RuleFor(x => x.Payment).NotNull().SetValidator(new RegisterSupplierPaymentCommandValidator());
    }
}

public sealed class FulfillCashFundingRequestValidator : AbstractValidator<FulfillCashFundingRequestCommand>
{
    public FulfillCashFundingRequestValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class RejectCashFundingRequestValidator : AbstractValidator<RejectCashFundingRequestCommand>
{
    public RejectCashFundingRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("El motivo del rechazo es obligatorio.")
            .MaximumLength(CashFundingRequest.ResolutionReasonMaxLen);
    }
}

public sealed class CancelCashFundingRequestValidator : AbstractValidator<CancelCashFundingRequestCommand>
{
    public CancelCashFundingRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("El motivo de la cancelación es obligatorio.")
            .MaximumLength(CashFundingRequest.ResolutionReasonMaxLen);
    }
}

// ── Mensajes ─────────────────────────────────────────────────────────────

internal static class CashFundingRequestMessages
{
    public const string NotFound = "Solicitud de efectivo no encontrada.";
    public const string OwnCashRegister =
        "Usted opera esta caja: registre el pago directamente, sin solicitud de efectivo.";
    public const string ExactlyOneCashLine =
        "La solicitud de efectivo requiere exactamente una línea de efectivo (una sola caja); las demás líneas deben ser bancarias.";
    public const string NoOpenSession = "La caja seleccionada no tiene una sesión abierta.";
    public const string SessionNotOpen = "La caja de la solicitud ya no está abierta.";
    public const string OtherBranch = "La caja seleccionada no pertenece a la sucursal activa.";
    public const string AlreadyResolved = "La solicitud de efectivo ya fue resuelta y no puede volver a usarse.";
    public const string IntegrityFailure =
        "La intención de pago de la solicitud no es íntegra (versión o huella inválida): no se ejecuta.";
    public const string ClientRequestConflict =
        "Ya existe una solicitud con este identificador pero con datos distintos.";
    public const string OnlyRequesterCancels = "Solo quien solicitó el efectivo puede cancelar la solicitud.";
}

// ── Create ───────────────────────────────────────────────────────────────

/// <summary>
/// ZH-CASH-FUNDING-REQUEST-WORKFLOW-02E-C — transacción: CashSession FOR UPDATE (vía el núcleo) →
/// validación completa del pago "como si el cajero lo ejecutara ahora" (mismas reglas que el pago
/// directo, incluido el chequeo temprano de efectivo disponible, que NO reserva) → crear Pending.
/// Idempotente por ClientRequestId + PayloadHash.
/// </summary>
public sealed class CreateCashFundingRequestHandler
    : IRequestHandler<CreateCashFundingRequestCommand, Result<CashFundingRequestDto>>
{
    private readonly ICashFundingRequestRepository _requests;
    private readonly ICashSessionRepository _sessions;
    private readonly ISupplierPaymentRegistrar _registrar;
    private readonly IBranchAccessGuard _branchAccess;
    private readonly IUnitOfWork _uow;
    private readonly IDatabaseExceptionTranslator _dbEx;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;

    public CreateCashFundingRequestHandler(
        ICashFundingRequestRepository requests,
        ICashSessionRepository sessions,
        ISupplierPaymentRegistrar registrar,
        IBranchAccessGuard branchAccess,
        IUnitOfWork uow,
        IDatabaseExceptionTranslator dbEx,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentBranch b,
        ICurrentUser u
    )
    {
        _requests = requests;
        _sessions = sessions;
        _registrar = registrar;
        _branchAccess = branchAccess;
        _uow = uow;
        _dbEx = dbEx;
        _t = t;
        _c = c;
        _b = b;
        _u = u;
    }

    public async Task<Result<CashFundingRequestDto>> Handle(CreateCashFundingRequestCommand cmd, CancellationToken ct)
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;
        var userId = _u.UserId;

        var snapshot = CashFundingPaymentSnapshot.FromIntent(cmd.Payment);
        var hash = CashFundingPaymentSnapshot.ComputeHash(snapshot);

        // ── Idempotencia: mismo id + misma intención → la misma solicitud; distinta → conflicto ──
        var existing = await _requests.GetByClientRequestIdAsync(tenantId, cmd.ClientRequestId, ct);
        if (existing is not null)
            return SameIntent(existing, hash);

        // ── Forma: exactamente una línea de efectivo (una sola caja objetivo) ──
        var cashLines = cmd.Payment.MethodLines.Count(l => l.CashRegisterId is not null);
        var cashRegisterId = CashFundingPaymentSnapshot.SingleCashRegisterId(snapshot);
        if (cashLines != 1 || cashRegisterId is null)
            return Result<CashFundingRequestDto>.ValidationFailure(CashFundingRequestMessages.ExactlyOneCashLine);

        // Reglas sin locks ni efectos (mismo rechazo temprano que el pago directo).
        var intentError = await _registrar.PrevalidateAsync(cmd.Payment, ct);
        if (intentError is not null)
            return Result<CashFundingRequestDto>.ValidationFailure(intentError);

        await _uow.BeginTransactionAsync(ct);
        try
        {
            // Orden único de locks: CashSession primero (serializa Create contra Close/Fulfill).
            var session = await _sessions.GetOpenByCashRegisterForUpdateAsync(tenantId, cashRegisterId.Value, ct);
            if (session is null || session.CompanyId != companyId)
                return await FailAsync(Result<CashFundingRequestDto>.ValidationFailure(CashFundingRequestMessages.NoOpenSession), ct);
            if (session.IsControlledBy(userId))
                return await FailAsync(Result<CashFundingRequestDto>.ValidationFailure(CashFundingRequestMessages.OwnCashRegister), ct);
            if (session.BranchId != _b.BranchId)
                return await FailAsync(Result<CashFundingRequestDto>.ValidationFailure(CashFundingRequestMessages.OtherBranch), ct);
            var branchAccess = await _branchAccess.RequireBranchAsync(session.BranchId, ct);
            if (!branchAccess.IsSuccess)
                return await FailAsync(Result<CashFundingRequestDto>.Failure(branchAccess.Error!, branchAccess.Code), ct);

            // Validación completa del pago, ejecutado por quien controla hoy la sesión (el cajero
            // que atenderá): medios, destinos, referencia, CxP, proveedor, comprobante, sucursal y
            // efectivo disponible (chequeo temprano, sin reserva). Ningún efecto.
            var validation = await _registrar.ValidateAsync(
                cmd.Payment,
                new SupplierPaymentRegistrationContext(tenantId, companyId, session.BranchId, userId, session.UserId),
                ct
            );
            if (!validation.IsSuccess)
                return await FailAsync(
                    Result<CashFundingRequestDto>.Failure(validation.Error!, validation.Code),
                    ct
                );

            var request = CashFundingRequest.Create(
                tenantId,
                companyId,
                session.BranchId,
                cashRegisterId.Value,
                session.Id,
                snapshot.SupplierId,
                snapshot.TotalAmount,
                CashFundingPaymentSnapshot.CashAmountFor(snapshot, cashRegisterId.Value),
                userId,
                CashFundingPaymentSnapshot.Serialize(snapshot),
                CashFundingPaymentSnapshot.CurrentVersion,
                hash,
                cmd.ClientRequestId
            );
            await _requests.AddAsync(request, ct);

            try
            {
                await _uow.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (_dbEx.TryGetUniqueViolation(ex, out _))
            {
                // Carrera con un reintento concurrente del mismo ClientRequestId.
                await _uow.RollbackAsync(ct);
                var raced = await _requests.GetByClientRequestIdAsync(tenantId, cmd.ClientRequestId, ct);
                return raced is null
                    ? Result<CashFundingRequestDto>.Conflict(CashFundingRequestMessages.ClientRequestConflict)
                    : SameIntent(raced, hash);
            }

            await _uow.CommitAsync(ct);
            return Result<CashFundingRequestDto>.Success(CashFundingRequestDto.From(request), ApiResponseCodes.Common.Created);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    private static Result<CashFundingRequestDto> SameIntent(CashFundingRequest existing, string hash) =>
        string.Equals(existing.PayloadHash, hash, StringComparison.Ordinal)
            ? Result<CashFundingRequestDto>.Success(CashFundingRequestDto.From(existing))
            : Result<CashFundingRequestDto>.Conflict(CashFundingRequestMessages.ClientRequestConflict);

    private async Task<Result<CashFundingRequestDto>> FailAsync(Result<CashFundingRequestDto> failure, CancellationToken ct)
    {
        await _uow.RollbackAsync(ct);
        return failure;
    }
}

// ── Fulfill ──────────────────────────────────────────────────────────────

/// <summary>
/// ZH-CASH-FUNDING-REQUEST-WORKFLOW-02E-C — el cajero entrega el efectivo. Una sola transacción:
/// CashSession FOR UPDATE → validar sesión/ownership/sucursal → CashFundingRequest FOR UPDATE →
/// Pending → versión + huella (fail-closed) → núcleo de pagos (originador = solicitante, ejecutor =
/// cajero) que revalida TODO y crea pago + CashMovement + posting (+ SupplierCredit) →
/// request.Fulfill → commit. Cualquier fallo revierte todo y la solicitud queda Pending.
/// Un reintento sobre una solicitud ya Fulfilled devuelve la misma (con su pago) sin re-ejecutar.
/// </summary>
public sealed class FulfillCashFundingRequestHandler
    : IRequestHandler<FulfillCashFundingRequestCommand, Result<CashFundingRequestDto>>
{
    private readonly ICashFundingRequestRepository _requests;
    private readonly ICashSessionRepository _sessions;
    private readonly ISupplierPaymentRegistrar _registrar;
    private readonly IBranchAccessGuard _branchAccess;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;

    public FulfillCashFundingRequestHandler(
        ICashFundingRequestRepository requests,
        ICashSessionRepository sessions,
        ISupplierPaymentRegistrar registrar,
        IBranchAccessGuard branchAccess,
        IUnitOfWork uow,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentBranch b,
        ICurrentUser u
    )
    {
        _requests = requests;
        _sessions = sessions;
        _registrar = registrar;
        _branchAccess = branchAccess;
        _uow = uow;
        _t = t;
        _c = c;
        _b = b;
        _u = u;
    }

    public async Task<Result<CashFundingRequestDto>> Handle(FulfillCashFundingRequestCommand cmd, CancellationToken ct)
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;
        var userId = _u.UserId;

        var current = await _requests.GetByIdAsync(tenantId, cmd.Id, ct);
        if (current is null)
            return Result<CashFundingRequestDto>.NotFound(CashFundingRequestMessages.NotFound);
        // Idempotencia de respuesta: un reintento no vuelve a pagar (terminal = inmutable).
        if (current.Status == CashFundingRequestStatus.Fulfilled)
            return Result<CashFundingRequestDto>.Success(CashFundingRequestDto.From(current));
        if (!current.IsPending)
            return Result<CashFundingRequestDto>.ValidationFailure(CashFundingRequestMessages.AlreadyResolved);

        await _uow.BeginTransactionAsync(ct);
        try
        {
            var sessionCheck = await CashFundingRequestLocks.LockControlledSessionAsync(
                _sessions, _branchAccess, tenantId, companyId, _b.BranchId, userId, current, ct);
            if (sessionCheck is not null)
                return await FailAsync(sessionCheck, ct);

            var request = await _requests.GetByIdForUpdateAsync(tenantId, cmd.Id, ct);
            if (request is null)
                return await FailAsync(Result<CashFundingRequestDto>.NotFound(CashFundingRequestMessages.NotFound), ct);
            if (request.Status == CashFundingRequestStatus.Fulfilled)
            {
                await _uow.RollbackAsync(ct);
                return Result<CashFundingRequestDto>.Success(CashFundingRequestDto.From(request));
            }
            if (!request.IsPending)
                return await FailAsync(Result<CashFundingRequestDto>.ValidationFailure(CashFundingRequestMessages.AlreadyResolved), ct);

            // ── Integridad de la intención (fail-closed): versión conocida + huella idéntica ──
            var snapshot = ReadSnapshot(request);
            if (snapshot is null)
                return await FailAsync(Result<CashFundingRequestDto>.ValidationFailure(CashFundingRequestMessages.IntegrityFailure), ct);

            var registration = await _registrar.RegisterAsync(
                CashFundingPaymentSnapshot.ToIntent(snapshot),
                new SupplierPaymentRegistrationContext(tenantId, companyId, request.BranchId, request.RequestedByUserId, userId),
                ct
            );
            if (!registration.IsSuccess)
                return await FailAsync(
                    Result<CashFundingRequestDto>.Failure(registration.Error!, registration.Code),
                    ct
                );

            request.Fulfill(userId, registration.Value!.Payment.Id);
            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);
            return Result<CashFundingRequestDto>.Success(CashFundingRequestDto.From(request));
        }
        catch (InvalidOperationException ex)
        {
            await _uow.RollbackAsync(ct);
            return Result<CashFundingRequestDto>.ValidationFailure(ex.Message);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>Snapshot V1 solo si la versión es conocida, el JSON es válido y la huella coincide; si no, null.</summary>
    private static CashFundingPaymentSnapshotV1? ReadSnapshot(CashFundingRequest request)
    {
        if (request.PayloadVersion != CashFundingPaymentSnapshot.CurrentVersion)
            return null;
        try
        {
            var snapshot = CashFundingPaymentSnapshot.Deserialize(request.PaymentPayload, request.PayloadVersion);
            var consistent =
                string.Equals(CashFundingPaymentSnapshot.ComputeHash(snapshot), request.PayloadHash, StringComparison.Ordinal)
                && snapshot.SupplierId == request.SupplierId
                && CashFundingPaymentSnapshot.SingleCashRegisterId(snapshot) == request.CashRegisterId
                && CashFundingPaymentSnapshot.CashAmountFor(snapshot, request.CashRegisterId) == request.CashAmount;
            return consistent ? snapshot : null;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    private async Task<Result<CashFundingRequestDto>> FailAsync(Result<CashFundingRequestDto> failure, CancellationToken ct)
    {
        await _uow.RollbackAsync(ct);
        return failure;
    }
}

// ── Reject ───────────────────────────────────────────────────────────────

/// <summary>ZH-CASH-FUNDING-REQUEST-WORKFLOW-02E-C — rechazo por el cajero. Locks: CashSession → request.</summary>
public sealed class RejectCashFundingRequestHandler
    : IRequestHandler<RejectCashFundingRequestCommand, Result<CashFundingRequestDto>>
{
    private readonly ICashFundingRequestRepository _requests;
    private readonly ICashSessionRepository _sessions;
    private readonly IBranchAccessGuard _branchAccess;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;

    public RejectCashFundingRequestHandler(
        ICashFundingRequestRepository requests,
        ICashSessionRepository sessions,
        IBranchAccessGuard branchAccess,
        IUnitOfWork uow,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentBranch b,
        ICurrentUser u
    )
    {
        _requests = requests;
        _sessions = sessions;
        _branchAccess = branchAccess;
        _uow = uow;
        _t = t;
        _c = c;
        _b = b;
        _u = u;
    }

    public async Task<Result<CashFundingRequestDto>> Handle(RejectCashFundingRequestCommand cmd, CancellationToken ct)
    {
        var tenantId = _t.TenantId;
        var userId = _u.UserId;
        var current = await _requests.GetByIdAsync(tenantId, cmd.Id, ct);
        if (current is null)
            return Result<CashFundingRequestDto>.NotFound(CashFundingRequestMessages.NotFound);
        if (!current.IsPending)
            return Result<CashFundingRequestDto>.ValidationFailure(CashFundingRequestMessages.AlreadyResolved);

        await _uow.BeginTransactionAsync(ct);
        try
        {
            var sessionCheck = await CashFundingRequestLocks.LockControlledSessionAsync(
                _sessions, _branchAccess, tenantId, _c.CompanyId, _b.BranchId, userId, current, ct);
            if (sessionCheck is not null)
            {
                await _uow.RollbackAsync(ct);
                return sessionCheck;
            }

            var request = await _requests.GetByIdForUpdateAsync(tenantId, cmd.Id, ct);
            if (request is null || !request.IsPending)
            {
                await _uow.RollbackAsync(ct);
                return Result<CashFundingRequestDto>.ValidationFailure(CashFundingRequestMessages.AlreadyResolved);
            }

            request.Reject(userId, cmd.Reason);
            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);
            return Result<CashFundingRequestDto>.Success(CashFundingRequestDto.From(request));
        }
        catch (ArgumentException ex)
        {
            await _uow.RollbackAsync(ct);
            return Result<CashFundingRequestDto>.ValidationFailure(ex.Message);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }
}

// ── Cancel ───────────────────────────────────────────────────────────────

/// <summary>
/// ZH-CASH-FUNDING-REQUEST-WORKFLOW-02E-C — cancelación por el solicitante (solo él). Mismo orden
/// de locks (CashSession → request) aunque no controle la caja, para no competir mal con
/// Fulfill/Close.
/// </summary>
public sealed class CancelCashFundingRequestHandler
    : IRequestHandler<CancelCashFundingRequestCommand, Result<CashFundingRequestDto>>
{
    private readonly ICashFundingRequestRepository _requests;
    private readonly ICashSessionRepository _sessions;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public CancelCashFundingRequestHandler(
        ICashFundingRequestRepository requests,
        ICashSessionRepository sessions,
        IUnitOfWork uow,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _requests = requests;
        _sessions = sessions;
        _uow = uow;
        _t = t;
        _u = u;
    }

    public async Task<Result<CashFundingRequestDto>> Handle(CancelCashFundingRequestCommand cmd, CancellationToken ct)
    {
        var tenantId = _t.TenantId;
        var userId = _u.UserId;
        var current = await _requests.GetByIdAsync(tenantId, cmd.Id, ct);
        if (current is null)
            return Result<CashFundingRequestDto>.NotFound(CashFundingRequestMessages.NotFound);
        if (current.RequestedByUserId != userId)
            return Result<CashFundingRequestDto>.Forbidden(CashFundingRequestMessages.OnlyRequesterCancels);
        if (!current.IsPending)
            return Result<CashFundingRequestDto>.ValidationFailure(CashFundingRequestMessages.AlreadyResolved);

        await _uow.BeginTransactionAsync(ct);
        try
        {
            await _sessions.GetByIdForUpdateAsync(tenantId, current.CashSessionId, ct);
            var request = await _requests.GetByIdForUpdateAsync(tenantId, cmd.Id, ct);
            if (request is null || !request.IsPending)
            {
                await _uow.RollbackAsync(ct);
                return Result<CashFundingRequestDto>.ValidationFailure(CashFundingRequestMessages.AlreadyResolved);
            }

            request.Cancel(userId, cmd.Reason);
            await _uow.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);
            return Result<CashFundingRequestDto>.Success(CashFundingRequestDto.From(request));
        }
        catch (ArgumentException ex)
        {
            await _uow.RollbackAsync(ct);
            return Result<CashFundingRequestDto>.ValidationFailure(ex.Message);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }
}

// ── Locks compartidos ────────────────────────────────────────────────────

internal static class CashFundingRequestLocks
{
    /// <summary>
    /// Primer lock del orden único (CashSession FOR UPDATE) + validaciones del cajero: la sesión es la
    /// de la solicitud, sigue abierta, es de la empresa y la sucursal de la solicitud, la sucursal
    /// activa es esa (guard oficial) y el usuario actual la controla. Devuelve el rechazo o null.
    /// </summary>
    public static async Task<Result<CashFundingRequestDto>?> LockControlledSessionAsync(
        ICashSessionRepository sessions,
        IBranchAccessGuard branchAccess,
        Guid tenantId,
        Guid companyId,
        Guid activeBranchId,
        Guid userId,
        CashFundingRequest request,
        CancellationToken ct
    )
    {
        var session = await sessions.GetByIdForUpdateAsync(tenantId, request.CashSessionId, ct);
        if (
            session is null
            || session.CompanyId != companyId
            || session.CashRegisterId != request.CashRegisterId
            || session.BranchId != request.BranchId
        )
            return Result<CashFundingRequestDto>.NotFound(CashFundingRequestMessages.NotFound);
        if (!session.IsOpen)
            return Result<CashFundingRequestDto>.ValidationFailure(CashFundingRequestMessages.SessionNotOpen);
        if (session.BranchId != activeBranchId)
            return Result<CashFundingRequestDto>.ValidationFailure(CashFundingRequestMessages.OtherBranch);
        var access = await branchAccess.RequireBranchAsync(session.BranchId, ct);
        if (!access.IsSuccess)
            return Result<CashFundingRequestDto>.Failure(access.Error!, access.Code);
        if (!session.IsControlledBy(userId))
            return Result<CashFundingRequestDto>.ValidationFailure(CashSessionOwnership.RejectionMessage(session));
        return null;
    }
}
