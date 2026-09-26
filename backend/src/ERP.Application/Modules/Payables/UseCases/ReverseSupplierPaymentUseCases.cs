using ERP.Application.Common;
using ERP.Application.Modules.Branches;
using ERP.Application.Modules.Caja;
using ERP.Application.Modules.Payables.Exceptions;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentValidation;
using MediatR;
using System.Security.Cryptography;
using System.Text;

namespace ERP.Application.Modules.Payables.UseCases;

// ── Request (POST body) ──────────────────────────────────────────────────

/// <summary>
/// SUPPLIER-PAYMENTS-REVERSE-16 — contrato HTTP de <c>POST /api/v1/supplier-payments/{id}/reverse</c>.
/// ZH-SUPPLIER-PAYMENT-REVERSAL-SEMANTICS-02B-FINAL — la reversa es una CORRECCIÓN DOCUMENTAL de una
/// operación que no llegó a ejecutarse, nunca la devolución de dinero ya entregado/transferido (eso
/// será un futuro <c>SupplierPaymentRefund</c>): <see cref="CashNotDeliveredConfirmed"/> = "Confirmo
/// que el efectivo no fue entregado al proveedor y permanece en la misma caja" (obligatorio si hay
/// fuentes de caja); <see cref="BankReversalReason"/> obligatorio si hay fuentes bancarias.
/// </summary>
public sealed record ReverseSupplierPaymentRequest(
    string Reason,
    bool CashNotDeliveredConfirmed = false,
    SupplierPaymentBankReversalReason? BankReversalReason = null
);

// ── Command ─────────────────────────────────────────────────────────────

/// <summary>
/// SUPPLIER-PAYMENTS-REVERSE-16 — reversa un pago a proveedor ya confirmado: revierte el saldo
/// aplicado en cada <c>AccountsPayableInstallment</c> afectada, recalcula la cabecera
/// <c>AccountsPayable</c> y genera el asiento contable inverso, todo en una sola transacción
/// explícita (si algo falla, nada queda parcial: el pago sigue <c>Confirmed</c>, los saldos no
/// cambian, no hay asiento parcial). Independiente de <c>ReverseCollectionCommand</c>
/// (Payment/PaymentApplicationLine, Collections/CxC) — no lo reutiliza ni lo toca.
/// ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — si el pago originó un anticipo
/// (<c>SupplierCredit</c>), la reversa solo procede mientras ese crédito siga íntegro
/// (<c>AvailableAmount == OriginalAmount</c>) y lo anula en la misma transacción con un movimiento
/// de sistema <c>SourcePaymentReversed</c> (nunca se borra).
/// </summary>
public sealed record ReverseSupplierPaymentCommand(
    Guid SupplierPaymentId,
    string Reason,
    bool CashNotDeliveredConfirmed = false,
    SupplierPaymentBankReversalReason? BankReversalReason = null
)
    : IRequest<Result<SupplierPaymentDto>>,
        ICompanyScopedRequest;

// ── Validator ───────────────────────────────────────────────────────────

public sealed class ReverseSupplierPaymentCommandValidator : AbstractValidator<ReverseSupplierPaymentCommand>
{
    public ReverseSupplierPaymentCommandValidator()
    {
        RuleFor(x => x.SupplierPaymentId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("El motivo del reverso es obligatorio.");
    }
}

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class ReverseSupplierPaymentCommandHandler
    : IRequestHandler<ReverseSupplierPaymentCommand, Result<SupplierPaymentDto>>
{
    private readonly ISupplierPaymentRepository _supplierPayments;
    private readonly IAccountsPayableRepository _accountsPayables;
    private readonly ICashSessionRepository _cashSessions;
    private readonly ISupplierCreditRepository _supplierCredits;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentBranch _b;
    private readonly IBranchAccessGuard _branchAccess;
    private readonly ICurrentUser _u;

    public ReverseSupplierPaymentCommandHandler(
        ISupplierPaymentRepository supplierPayments,
        IAccountsPayableRepository accountsPayables,
        ICashSessionRepository cashSessions,
        ISupplierCreditRepository supplierCredits,
        IUnitOfWork uow,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentBranch b,
        IBranchAccessGuard branchAccess,
        ICurrentUser u
    )
    {
        _supplierPayments = supplierPayments;
        _accountsPayables = accountsPayables;
        _cashSessions = cashSessions;
        _supplierCredits = supplierCredits;
        _uow = uow;
        _t = t;
        _c = c;
        _b = b;
        _branchAccess = branchAccess;
        _u = u;
    }

    public async Task<Result<SupplierPaymentDto>> Handle(
        ReverseSupplierPaymentCommand cmd,
        CancellationToken ct
    )
    {
        var tenantId = _t.TenantId;
        var companyId = _c.CompanyId;
        var userId = _u.UserId;

        await _uow.BeginTransactionAsync(ct);
        try
        {
            var payment = await _supplierPayments.GetByIdAsync(tenantId, cmd.SupplierPaymentId, ct);
            if (payment is null || payment.CompanyId != companyId)
            {
                await _uow.RollbackAsync(ct);
                return Result<SupplierPaymentDto>.NotFound("Pago a proveedor no encontrado.");
            }

            // ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — anticipo originado por el remanente. Lock B
            // (SupplierCredit.Lock) ANTES de cualquier lock de caja — mismo orden que
            // RegisterSupplierCreditRefund (Lock B → CashSession FOR UPDATE), nunca deadlock en
            // cruz. Descubrimiento sin tracking + recarga tras el lock = lectura fresca real.
            SupplierCredit? advance = null;
            if (payment.UnappliedAmount > 0)
            {
                var advanceId = await _supplierCredits.GetIdBySourceSupplierPaymentIdAsync(
                    tenantId,
                    payment.Id,
                    ct
                );
                if (advanceId is null)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierPaymentDto>.ValidationFailure(
                        "No se encontró el anticipo generado por este pago: no puede reversarse."
                    );
                }
                await _supplierCredits.AcquireLockAsync(tenantId, advanceId.Value, ct);
                advance = await _supplierCredits.GetByIdAsync(tenantId, advanceId.Value, ct);
                if (advance is null || !advance.IsIntact)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierPaymentDto>.ValidationFailure(
                        "No se puede reversar el pago porque el anticipo que generó ya fue aplicado o reembolsado. Revierta primero esas operaciones del crédito de proveedor."
                    );
                }
            }

            // Dominio valida: Status debe ser Confirmed (bloquea doble reversa) y el motivo no
            // puede estar vacío.
            try
            {
                payment.Reverse(
                    cmd.Reason,
                    userId,
                    DateTime.UtcNow,
                    cmd.CashNotDeliveredConfirmed,
                    cmd.BankReversalReason
                );
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                await _uow.RollbackAsync(ct);
                return Result<SupplierPaymentDto>.ValidationFailure(ex.Message);
            }

            // ZH-SUPPLIER-PAYMENT-CASH-TRANSFER-HARDENING-02A — por cada fuente de caja que sacó
            // efectivo (CashMovementId vinculado), ingreso compensatorio en la sesión Open actual de
            // esa caja. El egreso original nunca se borra ni se modifica; la trazabilidad queda por
            // ReferenceType/ReferenceId = SupplierPayment. Pagos anteriores a 02A (sin movimiento
            // original) no generan compensación — nunca hubo efecto en caja que deshacer.
            // 02B-CLOSE — sucursal activa obligatoria SOLO si la reversa produce un efecto
            // operativo en caja. El comando sigue siendo company-scoped (una reversa bancaria no
            // necesita sucursal), así que BranchScopeBehavior no valida el header aquí: se valida
            // explícitamente con el mismo guard oficial (IBranchAccessGuard), nunca confiando en el
            // X-Branch-Id crudo.
            var hasCashEffect = payment.MethodLines.Any(l => l.CashRegisterId is not null);
            if (hasCashEffect)
            {
                if (!_b.HasBranchContext)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierPaymentDto>.ValidationFailure(
                        "La reversa devuelve efectivo a una caja: seleccione la sucursal activa de esa caja."
                    );
                }
                var branchAccess = await _branchAccess.RequireBranchAsync(_b.BranchId, ct);
                if (!branchAccess.IsSuccess)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierPaymentDto>.ValidationFailure(
                        branchAccess.Error ?? "No tiene acceso a la sucursal activa."
                    );
                }
            }

            // 02B-FINAL — reversa documental de efectivo: el efectivo nunca se entregó y sigue en la
            // MISMA sesión de la que salió. Se actúa únicamente sobre la sesión ORIGINAL de cada línea
            // (CashSessionId guardado en 02A) — nunca sobre otra sesión abierta de la misma caja —, que
            // debe seguir abierta, controlada por el usuario actual y en la sucursal activa. Lock
            // oficial FOR UPDATE, en el mismo orden determinista por CashRegisterId que el registro
            // del pago (02A-FINAL): nunca deadlock entre reversas y pagos. La confirmación explícita
            // (CashNotDeliveredConfirmed) y la trazabilidad de la línea ya las exigió el dominio.
            var originalSessions = new Dictionary<Guid, CashSession>();
            foreach (var cashLine in payment.MethodLines
                .Where(l => l.CashRegisterId is not null)
                .GroupBy(l => l.CashSessionId!.Value)
                .Select(g => g.First())
                .OrderBy(l => l.CashRegisterId!.Value)
                .ThenBy(l => l.CashSessionId!.Value))
            {
                var sessionId = cashLine.CashSessionId!.Value;
                var session = await _cashSessions.GetByIdForUpdateAsync(tenantId, sessionId, ct);
                if (session is null || session.CompanyId != companyId)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierPaymentDto>.ValidationFailure(
                        "No se encontró la sesión de caja de la que salió el efectivo."
                    );
                }
                if (!session.IsOpen)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierPaymentDto>.ValidationFailure(
                        "El efectivo salió de una sesión que ya está cerrada. Si el proveedor devolvió el dinero, registre una devolución de fondos."
                    );
                }
                // 02B — solo quien opera la sesión original puede recibir de vuelta ese efectivo.
                if (!session.IsControlledBy(userId))
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierPaymentDto>.ValidationFailure(CashSessionOwnership.RejectionMessage(session));
                }
                // 02B-CLOSE — misma regla de sucursal que el registro del pago.
                if (session.BranchId != _b.BranchId)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierPaymentDto>.ValidationFailure(
                        "La caja seleccionada no pertenece a la sucursal activa."
                    );
                }
                originalSessions[sessionId] = session;
            }

            // Ingreso compensatorio ÚNICAMENTE en la sesión original de cada línea; el egreso
            // original nunca se borra (trazabilidad por ReferenceType/ReferenceId = SupplierPayment).
            foreach (var methodLine in payment.MethodLines.Where(l => l.CashRegisterId is not null))
            {
                var session = originalSessions[methodLine.CashSessionId!.Value];
                try
                {
                    session.RecordMovement(
                        CashMovementType.SupplierPaymentReversal,
                        methodLine.Amount,
                        $"Reversa de pago a proveedor {payment.SystemNumber}",
                        userId,
                        CashReferenceType.SupplierPayment,
                        payment.Id,
                        payment.SystemNumber
                    );
                }
                catch (InvalidOperationException ex)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierPaymentDto>.ValidationFailure(ex.Message);
                }
            }

            if (advance is not null)
            {
                try
                {
                    // ClientRequestId = Id del pago: la propia reversa (Confirmed → Reversed, una sola
                    // vez) garantiza unicidad; huella determinista del movimiento de sistema.
                    advance.RegisterSourcePaymentReversal(
                        userId,
                        payment.Id,
                        ComputeSourcePaymentReversalHash(advance.Id, payment.Id)
                    );
                }
                catch (InvalidOperationException ex)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierPaymentDto>.ValidationFailure(ex.Message);
                }
            }

            // Revierte, cuota por cuota, exactamente lo que esa línea aplicó — nunca por FIFO.
            foreach (var appLine in payment.ApplicationLines)
            {
                var payable = await _accountsPayables.GetByInstallmentIdAsync(
                    tenantId,
                    appLine.AccountsPayableInstallmentId,
                    ct
                );
                if (payable is null)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierPaymentDto>.ValidationFailure(
                        $"La cuota {appLine.AccountsPayableInstallmentId} ya no existe."
                    );
                }

                try
                {
                    payable.ReversePaymentToInstallment(
                        appLine.AccountsPayableInstallmentId,
                        appLine.AmountApplied,
                        userId
                    );
                }
                catch (InvalidOperationException ex)
                {
                    await _uow.RollbackAsync(ct);
                    return Result<SupplierPaymentDto>.ValidationFailure(ex.Message);
                }
            }

            try
            {
                // SUPPLIER-PAYMENTS-REVERSE-16: SaveChangesAsync publica SupplierPaymentReversedEvent
                // ANTES del commit (ErpDbContext.SaveChangesAsync, ADR-026 §8) —
                // SupplierPaymentReversedPostingTranslator lanza SupplierPaymentPostingFailedException
                // si el asiento inverso no puede generarse. El catch de abajo revierte la
                // transacción completa: el pago sigue Confirmed, los saldos de
                // AccountsPayableInstallment mutados arriba nunca llegan a persistirse.
                await _supplierPayments.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
            {
                await _uow.RollbackAsync(ct);
                return Result<SupplierPaymentDto>.ValidationFailure(
                    "El pago o una de las cuentas por pagar afectadas fue modificado concurrentemente. Intente nuevamente."
                );
            }
            catch (SupplierPaymentPostingFailedException ex)
            {
                await _uow.RollbackAsync(ct);
                return Result<SupplierPaymentDto>.ValidationFailure(ex.Message, ex.Code);
            }

            await _uow.CommitAsync(ct);
            return Result<SupplierPaymentDto>.Success(
                SupplierPaymentDtoMapper.ToDto(payment, supplierCreditId: advance?.Id)
            );
        }
        catch (InvalidOperationException ex)
        {
            await _uow.RollbackAsync(ct);
            return Result<SupplierPaymentDto>.ValidationFailure(ex.Message);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>Huella determinista del movimiento de sistema <c>SourcePaymentReversed</c>.</summary>
    private static string ComputeSourcePaymentReversalHash(Guid supplierCreditId, Guid supplierPaymentId)
    {
        var canonical = string.Join(
            "",
            "SourcePaymentReversal",
            supplierCreditId.ToString("D"),
            supplierPaymentId.ToString("D")
        );
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
