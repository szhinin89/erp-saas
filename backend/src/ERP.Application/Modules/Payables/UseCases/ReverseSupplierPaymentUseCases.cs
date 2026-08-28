using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Payables.Exceptions;
using ERP.Domain.Modules.Payables.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Payables.UseCases;

// ── Request (POST body) ──────────────────────────────────────────────────

/// <summary>SUPPLIER-PAYMENTS-REVERSE-16 — contrato HTTP de <c>POST /api/v1/supplier-payments/{id}/reverse</c>.</summary>
public sealed record ReverseSupplierPaymentRequest(string Reason);

// ── Command ─────────────────────────────────────────────────────────────

/// <summary>
/// SUPPLIER-PAYMENTS-REVERSE-16 — reversa un pago a proveedor ya confirmado: revierte el saldo
/// aplicado en cada <c>AccountsPayableInstallment</c> afectada, recalcula la cabecera
/// <c>AccountsPayable</c> y genera el asiento contable inverso, todo en una sola transacción
/// explícita (si algo falla, nada queda parcial: el pago sigue <c>Confirmed</c>, los saldos no
/// cambian, no hay asiento parcial). Independiente de <c>ReverseCollectionCommand</c>
/// (Payment/PaymentApplicationLine, Collections/CxC) — no lo reutiliza ni lo toca.
/// </summary>
public sealed record ReverseSupplierPaymentCommand(Guid SupplierPaymentId, string Reason)
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
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public ReverseSupplierPaymentCommandHandler(
        ISupplierPaymentRepository supplierPayments,
        IAccountsPayableRepository accountsPayables,
        IUnitOfWork uow,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _supplierPayments = supplierPayments;
        _accountsPayables = accountsPayables;
        _uow = uow;
        _t = t;
        _c = c;
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

            // Dominio valida: Status debe ser Confirmed (bloquea doble reversa) y el motivo no
            // puede estar vacío.
            try
            {
                payment.Reverse(cmd.Reason, userId, DateTime.UtcNow);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                await _uow.RollbackAsync(ct);
                return Result<SupplierPaymentDto>.ValidationFailure(ex.Message);
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
            return Result<SupplierPaymentDto>.Success(SupplierPaymentDtoMapper.ToDto(payment));
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
}
