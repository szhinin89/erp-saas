using ERP.Application.Common;
using ERP.Application.Modules.Caja.FundingRequests;
using ERP.Application.Modules.Payables.Services;
using ERP.Domain.Common;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Payables.UseCases;

// ── DTOs (Request, línea por línea) ─────────────────────────────────────

/// <summary>
/// SUPPLIER-PAYMENTS-REGISTER-15C — un medio de pago usado en el registro.
/// ZH-SUPPLIER-PAYMENT-CASH-TRANSFER-HARDENING-02A — <see cref="TransactionDate"/>: fecha efectiva
/// real de una fuente bancaria — obligatoria y explícita (02A-FINAL: nunca completada con
/// PaymentDate); prohibida en fuentes de caja. <see cref="ReferenceNumber"/> es el único número de operación bancaria.
/// </summary>
public sealed record SupplierPaymentMethodLineRequest(
    Guid PaymentMethodId,
    Guid? CompanyBankAccountId,
    Guid? CashRegisterId,
    decimal Amount,
    string? ReferenceNumber = null,
    string? CheckNumber = null,
    DateOnly? CheckDate = null,
    string? Notes = null,
    DateOnly? TransactionDate = null
);

/// <summary>SUPPLIER-PAYMENTS-REGISTER-15C — una aplicación a cuota de <c>AccountsPayableInstallment</c>.</summary>
public sealed record SupplierPaymentApplicationLineRequest(
    Guid AccountsPayableInstallmentId,
    decimal AmountApplied
);

/// <summary>
/// SUPPLIER-PAYMENTS-REGISTER-15C — celda de la matriz medio↔cuota. Los índices referencian
/// posiciones dentro de <see cref="RegisterSupplierPaymentRequest.MethodLines"/>/
/// <see cref="RegisterSupplierPaymentRequest.ApplicationLines"/> de la misma request.
/// </summary>
public sealed record SupplierPaymentAllocationLineRequest(
    int MethodLineIndex,
    int ApplicationLineIndex,
    decimal Amount
);

/// <summary>
/// SUPPLIER-PAYMENTS-REGISTER-15C — contrato HTTP de <c>POST /api/v1/supplier-payments</c>. Nunca
/// incluye TenantId/CompanyId/BranchId — vienen del contexto autenticado, nunca del body (regla
/// global de multi-tenant). El controller lo mapea 1:1 a <see cref="RegisterSupplierPaymentCommand"/>.
/// ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — <see cref="ConfirmUnappliedAmount"/>: confirmación
/// explícita del usuario de que el remanente no aplicado (TotalAmount − Σ aplicaciones) quedará como
/// anticipo a favor del proveedor. Obligatoria si hay remanente; el backend la revalida siempre.
/// </summary>
public sealed record RegisterSupplierPaymentRequest(
    Guid SupplierId,
    DateOnly PaymentDate,
    decimal TotalAmount,
    string? ReceiptNumber,
    IReadOnlyList<SupplierPaymentMethodLineRequest> MethodLines,
    IReadOnlyList<SupplierPaymentApplicationLineRequest>? ApplicationLines,
    IReadOnlyList<SupplierPaymentAllocationLineRequest>? Allocations,
    bool ConfirmUnappliedAmount = false,
    Guid ClientRequestId = default
);

// ── DTO de salida ─────────────────────────────────────────────────────────

public sealed record SupplierPaymentMethodLineDto(
    Guid Id,
    Guid PaymentMethodId,
    Guid? CompanyBankAccountId,
    Guid? CashRegisterId,
    decimal Amount,
    string? ReferenceNumber,
    string? CheckNumber,
    DateOnly? CheckDate,
    string? Notes,
    DateOnly? TransactionDate = null,
    Guid? CashSessionId = null,
    Guid? CashMovementId = null
);

/// <summary>
/// SUPPLIER-PAYMENT-DETAIL-APPLICATION-LINE-DISPLAY-NAMES-01 — <c>SupplierPaymentApplicationLine</c>
/// (dominio) solo guarda <see cref="AccountsPayableInstallmentId"/>/<see cref="AmountApplied"/>, sin
/// snapshot de documento — los campos de solo lectura de abajo (<see cref="DocumentNumber"/>,
/// <see cref="InstallmentNumber"/>, <see cref="DueDate"/>, <see cref="IssueDate"/>,
/// <see cref="OriginType"/>) se resuelven en el momento de la consulta contra
/// <see cref="ERP.Domain.Modules.Payables.Entities.AccountsPayable"/>/
/// <c>AccountsPayableInstallment</c> (mismo dato que ya expone <c>PayablesController</c>, nunca un
/// duplicado). Quedan <c>null</c> — nunca rompen el detalle — si la cuota ya no puede resolverse
/// (caso excepcional); el frontend cae a un fallback técnico con el Id crudo en ese caso.
/// </summary>
public sealed record SupplierPaymentApplicationLineDto(
    Guid Id,
    Guid AccountsPayableInstallmentId,
    decimal AmountApplied,
    string? DocumentNumber = null,
    int? InstallmentNumber = null,
    DateOnly? DueDate = null,
    DateOnly? IssueDate = null,
    string? OriginType = null
);

public sealed record SupplierPaymentAllocationLineDto(
    Guid Id,
    Guid SupplierPaymentMethodLineId,
    Guid SupplierPaymentApplicationLineId,
    decimal Amount
);

public sealed record SupplierPaymentDto(
    Guid Id,
    Guid SupplierId,
    Guid BranchId,
    DateOnly PaymentDate,
    decimal TotalAmount,
    string SystemNumber,
    string? ReceiptNumber,
    string DisplayNumber,
    string Status,
    IReadOnlyList<SupplierPaymentMethodLineDto> MethodLines,
    IReadOnlyList<SupplierPaymentApplicationLineDto> ApplicationLines,
    IReadOnlyList<SupplierPaymentAllocationLineDto> Allocations,
    DateTime CreatedAt,
    DateTime? ReversedAtUtc = null,
    Guid? ReversedBy = null,
    string? ReverseReason = null,
    string? ReversalBankReason = null,
    bool? ReversalCashNotDeliveredConfirmed = null,
    // ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — derivados (nunca persistidos) + anticipo originado.
    decimal AppliedAmount = 0m,
    decimal UnappliedAmount = 0m,
    Guid? SupplierCreditId = null
);

// ── Command ─────────────────────────────────────────────────────────────

/// <summary>
/// SUPPLIER-PAYMENTS-REGISTER-15C — registra y confirma un pago a proveedor en una sola operación
/// (sin Draft — SUPPLIER-PAYMENTS-AUDIT-15A/FOUNDATION-15B), aplicándolo contra una o más
/// <c>AccountsPayableInstallment</c>. Independiente de <c>RegisterCollectionCommand</c>
/// (Payment/PaymentApplicationLine, Collections/CxC) — no lo reutiliza ni lo toca.
/// PAYABLES-BRANCH-SCOPE-DECISION-01 — a diferencia de las queries de lectura de este módulo (ver
/// <c>GetSupplierPaymentByIdQuery</c>), este Command SÍ se mantiene <c>IBranchScopedRequest</c>
/// deliberadamente: <c>SupplierPayment.Create()</c> exige <c>branchId != Guid.Empty</c> como
/// invariante de dominio (registra desde qué sucursal se emitió el pago, por trazabilidad — no como
/// filtro de acceso), así que el handler necesita una sucursal activa válida para poder construir el
/// agregado, aunque la autorización real (destino financiero, cuota, CxP) siga siendo 100%
/// company-level. No mueve caja/banco por sucursal — ningún dato se filtra ni se restringe por
/// <c>BranchId</c>.
/// </summary>
/// <remarks>
/// ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — Σ aplicaciones ≤ TotalAmount. El remanente exige
/// <see cref="ConfirmUnappliedAmount"/> y genera un <c>SupplierCredit</c> (origen SupplierPayment)
/// por exactamente ese monto, en la misma transacción. Cero aplicaciones solo si la empresa tiene
/// activa <c>payables.allow_supplier_payment_without_payable</c>.
/// </remarks>
public sealed record RegisterSupplierPaymentCommand(
    Guid SupplierId,
    DateOnly PaymentDate,
    decimal TotalAmount,
    string? ReceiptNumber,
    IReadOnlyList<SupplierPaymentMethodLineRequest> MethodLines,
    IReadOnlyList<SupplierPaymentApplicationLineRequest> ApplicationLines,
    IReadOnlyList<SupplierPaymentAllocationLineRequest> Allocations,
    bool ConfirmUnappliedAmount = false,
    Guid ClientRequestId = default
) : IRequest<Result<SupplierPaymentDto>>, IBranchScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

public sealed class SupplierPaymentMethodLineRequestValidator
    : AbstractValidator<SupplierPaymentMethodLineRequest>
{
    public SupplierPaymentMethodLineRequestValidator()
    {
        RuleFor(x => x.PaymentMethodId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x)
            .Must(x => x.CompanyBankAccountId is not null ^ x.CashRegisterId is not null)
            .WithMessage("Debe especificar exactamente una cuenta bancaria o una caja destino.");
        RuleFor(x => x.TransactionDate)
            .Null()
            .When(x => x.CashRegisterId is not null)
            .WithMessage("La fecha de transacción bancaria no aplica a un medio de pago en caja.");
        // 02A-FINAL — explícita, nunca completada con PaymentDate en backend.
        RuleFor(x => x.TransactionDate)
            .NotNull()
            .When(x => x.CompanyBankAccountId is not null)
            .WithMessage("La fecha de la transacción bancaria es obligatoria.");
    }
}

public sealed class SupplierPaymentApplicationLineRequestValidator
    : AbstractValidator<SupplierPaymentApplicationLineRequest>
{
    public SupplierPaymentApplicationLineRequestValidator()
    {
        RuleFor(x => x.AccountsPayableInstallmentId).NotEmpty();
        RuleFor(x => x.AmountApplied).GreaterThan(0);
    }
}

public sealed class SupplierPaymentAllocationLineRequestValidator
    : AbstractValidator<SupplierPaymentAllocationLineRequest>
{
    public SupplierPaymentAllocationLineRequestValidator()
    {
        RuleFor(x => x.MethodLineIndex).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ApplicationLineIndex).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public sealed class RegisterSupplierPaymentCommandValidator
    : AbstractValidator<RegisterSupplierPaymentCommand>
{
    public RegisterSupplierPaymentCommandValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.PaymentDate).NotEmpty();
        RuleFor(x => x.TotalAmount).GreaterThan(0);
        RuleFor(x => x.MethodLines)
            .NotEmpty()
            .WithMessage("El pago debe tener al menos un medio de pago.");
        // ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — cero aplicaciones es válido en forma; si la
        // empresa lo permite lo decide el handler (política por empresa, no regla de formato).
        RuleFor(x => x.ApplicationLines).NotNull();
        RuleFor(x => x.Allocations).NotNull();
        RuleFor(x => x.Allocations)
            .NotEmpty()
            .When(x => x.ApplicationLines is { Count: > 0 })
            .WithMessage("El pago debe tener al menos una distribución medio↔cuota.");
        RuleFor(x => x.ApplicationLines)
            .Must((cmd, lines) => lines.Sum(l => l.AmountApplied) <= cmd.TotalAmount)
            .When(x => x.ApplicationLines is not null)
            .WithMessage("La suma aplicada a cuotas no puede superar el total del pago.");
        RuleForEach(x => x.MethodLines).SetValidator(new SupplierPaymentMethodLineRequestValidator());
        RuleForEach(x => x.ApplicationLines)
            .SetValidator(new SupplierPaymentApplicationLineRequestValidator());
        RuleForEach(x => x.Allocations).SetValidator(new SupplierPaymentAllocationLineRequestValidator());
    }
}

/// <summary>
/// ZH-FINANCIAL-COMMAND-IDEMPOTENCY-01 — el pago DIRECTO exige la intención del cliente. Validador
/// separado a propósito: <see cref="RegisterSupplierPaymentCommandValidator"/> valida la intención
/// de pago y también la reutiliza la solicitud de efectivo (que tiene su propio ClientRequestId).
/// </summary>
public sealed class RegisterSupplierPaymentClientRequestValidator : AbstractValidator<RegisterSupplierPaymentCommand>
{
    public RegisterSupplierPaymentClientRequestValidator() =>
        RuleFor(x => x.ClientRequestId).NotEmpty().WithMessage("El identificador de idempotencia es obligatorio.");
}

// ── Handler ─────────────────────────────────────────────────────────────

/// <summary>
/// Pago directo: el usuario actual prepara y ejecuta. ZH-CASH-FUNDING-REQUEST-FOUNDATION-02E-B —
/// solo gobierna la transacción; todas las reglas viven en <see cref="ISupplierPaymentRegistrar"/>
/// (núcleo compartido con la ejecución de solicitudes de efectivo), sin cambios de comportamiento.
/// <para>
/// ZH-FINANCIAL-COMMAND-IDEMPOTENCY-01 — una intención (<see cref="RegisterSupplierPaymentCommand.ClientRequestId"/>)
/// produce como máximo un pago: mismo id + misma huella → el pago ya registrado, sin volver a
/// aplicar CxP, mover caja, crear anticipo ni postear; mismo id + otra huella → Conflict. La
/// barrera definitiva es el índice único (TenantId, ClientRequestId). Si el intento falla por
/// cualquier motivo (índice único, cuota ya pagada por el ganador mientras esperaba el lock,
/// comprobante ya usado) y la intención quedó registrada por un reintento concurrente, la
/// transacción perdedora se revierte completa y se responde con el pago ganador.
/// </para>
/// </summary>
public sealed class RegisterSupplierPaymentCommandHandler
    : IRequestHandler<RegisterSupplierPaymentCommand, Result<SupplierPaymentDto>>
{
    internal const string ClientRequestConflict =
        "Ya existe un pago con este identificador pero con datos distintos.";

    private readonly ISupplierPaymentRegistrar _registrar;
    private readonly ISupplierPaymentRepository _payments;
    private readonly ISupplierCreditRepository _supplierCredits;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;

    public RegisterSupplierPaymentCommandHandler(
        ISupplierPaymentRegistrar registrar,
        ISupplierPaymentRepository payments,
        ISupplierCreditRepository supplierCredits,
        IUnitOfWork uow,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentBranch b,
        ICurrentUser u
    )
    {
        _registrar = registrar;
        _payments = payments;
        _supplierCredits = supplierCredits;
        _uow = uow;
        _t = t;
        _c = c;
        _b = b;
        _u = u;
    }

    public async Task<Result<SupplierPaymentDto>> Handle(
        RegisterSupplierPaymentCommand cmd,
        CancellationToken ct
    )
    {
        var userId = _u.UserId;
        var key = new ClientRequestKey(
            cmd.ClientRequestId,
            CashFundingPaymentSnapshot.ComputeHash(CashFundingPaymentSnapshot.FromIntent(cmd))
        );

        // Camino rápido del replay: ningún efecto, ni transacción.
        var existing = await _payments.GetByClientRequestIdAsync(_t.TenantId, key.Id, ct);
        if (existing is not null)
            return await ReplayAsync(existing, key, ct);

        // Rechazo sin ningún efecto (ni transacción) para las reglas que no requieren locks (02C).
        var intentError = await _registrar.PrevalidateAsync(cmd, ct);
        if (intentError is not null)
            return Result<SupplierPaymentDto>.ValidationFailure(intentError);

        await _uow.BeginTransactionAsync(ct);
        Result<SupplierPaymentRegistration> registration;
        try
        {
            registration = await _registrar.RegisterAsync(
                cmd,
                new SupplierPaymentRegistrationContext(_t.TenantId, _c.CompanyId, _b.BranchId, userId, userId),
                ct,
                key
            );
            if (registration.IsSuccess)
                await _uow.CommitAsync(ct);
            else
                await _uow.RollbackAsync(ct);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            var raced = await _payments.GetByClientRequestIdAsync(_t.TenantId, key.Id, ct);
            if (raced is null)
                throw;
            return await ReplayAsync(raced, key, ct);
        }

        if (!registration.IsSuccess)
        {
            var raced = await _payments.GetByClientRequestIdAsync(_t.TenantId, key.Id, ct);
            return raced is not null
                ? await ReplayAsync(raced, key, ct)
                : Result<SupplierPaymentDto>.Failure(registration.Error!, registration.Code);
        }

        var (payment, supplierCreditId) = registration.Value!;
        return Result<SupplierPaymentDto>.Success(
            SupplierPaymentDtoMapper.ToDto(payment, supplierCreditId: supplierCreditId),
            ApiResponseCodes.Common.Created
        );
    }

    /// <summary>Mismo request → la misma respuesta que la creación original; request distinto → Conflict.</summary>
    private async Task<Result<SupplierPaymentDto>> ReplayAsync(
        SupplierPayment existing,
        ClientRequestKey key,
        CancellationToken ct
    )
    {
        if (!key.Matches(existing.RequestPayloadHash))
            return Result<SupplierPaymentDto>.Conflict(ClientRequestConflict);

        var supplierCreditId = existing.UnappliedAmount > 0
            ? await _supplierCredits.GetIdBySourceSupplierPaymentIdAsync(_t.TenantId, existing.Id, ct)
            : null;
        return Result<SupplierPaymentDto>.Success(
            SupplierPaymentDtoMapper.ToDto(existing, supplierCreditId: supplierCreditId),
            ApiResponseCodes.Common.Created
        );
    }
}

// ── Mapping ─────────────────────────────────────────────────────────────

// SUPPLIER-PAYMENTS-FRONTEND-15E: internal (no longer file-scoped) para que
// GetSupplierPaymentUseCases.cs (misma capa, mismo namespace) reutilice el mismo mapeo — sin
// duplicar la fuente de verdad de "cómo se ve un SupplierPaymentDto".
/// <summary>
/// SUPPLIER-PAYMENT-DETAIL-APPLICATION-LINE-DISPLAY-NAMES-01 — datos de solo lectura de la cuota
/// (documento origen + cuota), resueltos contra <c>AccountsPayable</c>/<c>AccountsPayableInstallment</c>
/// para proyectar <see cref="SupplierPaymentApplicationLineDto"/> sin exponer el GUID crudo.
/// </summary>
internal sealed record InstallmentDisplayInfo(
    string DocumentNumber,
    int InstallmentNumber,
    DateOnly DueDate,
    DateOnly IssueDate,
    string OriginType
);

internal static class SupplierPaymentDtoMapper
{
    /// <summary>
    /// <paramref name="installmentDisplayInfo"/> es opcional (ausente para las respuestas de
    /// Register/Reverse, que no lo necesitan porque el frontend navega de inmediato al detalle,
    /// que sí lo resuelve) — sin él, <see cref="SupplierPaymentApplicationLineDto"/> simplemente
    /// deja sus campos de proyección en <c>null</c> (fallback técnico en el frontend).
    /// </summary>
    public static SupplierPaymentDto ToDto(
        SupplierPayment p,
        IReadOnlyDictionary<Guid, InstallmentDisplayInfo>? installmentDisplayInfo = null,
        Guid? supplierCreditId = null
    ) =>
        new(
            p.Id,
            p.SupplierId,
            p.BranchId,
            p.PaymentDate,
            p.TotalAmount,
            p.SystemNumber,
            p.ReceiptNumber,
            p.DisplayNumber,
            p.Status.ToString(),
            p.MethodLines
                .Select(l => new SupplierPaymentMethodLineDto(
                    l.Id,
                    l.PaymentMethodId,
                    l.CompanyBankAccountId,
                    l.CashRegisterId,
                    l.Amount,
                    l.ReferenceNumber,
                    l.CheckNumber,
                    l.CheckDate,
                    l.Notes,
                    l.TransactionDate,
                    l.CashSessionId,
                    l.CashMovementId
                ))
                .ToList(),
            p.ApplicationLines
                .Select(l =>
                {
                    InstallmentDisplayInfo? info = null;
                    installmentDisplayInfo?.TryGetValue(l.AccountsPayableInstallmentId, out info);
                    return new SupplierPaymentApplicationLineDto(
                        l.Id,
                        l.AccountsPayableInstallmentId,
                        l.AmountApplied,
                        info?.DocumentNumber,
                        info?.InstallmentNumber,
                        info?.DueDate,
                        info?.IssueDate,
                        info?.OriginType
                    );
                })
                .ToList(),
            p.AllocationLines
                .Select(l => new SupplierPaymentAllocationLineDto(
                    l.Id,
                    l.SupplierPaymentMethodLineId,
                    l.SupplierPaymentApplicationLineId,
                    l.Amount
                ))
                .ToList(),
            p.CreatedAt,
            p.ReversedAtUtc,
            p.ReversedBy,
            p.ReverseReason,
            p.ReversalBankReason?.ToString(),
            p.ReversalCashNotDeliveredConfirmed,
            p.AppliedAmount,
            p.UnappliedAmount,
            supplierCreditId
        );
}
