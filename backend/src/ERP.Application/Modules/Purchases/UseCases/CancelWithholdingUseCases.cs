using ERP.Application.Common;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Purchases.UseCases;

// ── Command ─────────────────────────────────────────────────────────────

public sealed record CancelWithholdingCommand(Guid WithholdingId, string Reason)
    : IRequest<Result<IssuedWithholdingDto>>,
        IBranchScopedRequest;

// ── Validator ───────────────────────────────────────────────────────────

public sealed class CancelWithholdingValidator : AbstractValidator<CancelWithholdingCommand>
{
    public CancelWithholdingValidator()
    {
        RuleFor(x => x.WithholdingId).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(300)
            .WithMessage("El motivo de anulación es obligatorio (máximo 300 caracteres).");
    }
}

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class CancelWithholdingHandler
    : IRequestHandler<CancelWithholdingCommand, Result<IssuedWithholdingDto>>
{
    private readonly IPurchaseInvoiceRepository _repo;
    private readonly IPurchaseReturnRepository _purchaseReturnRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public CancelWithholdingHandler(
        IPurchaseInvoiceRepository repo,
        IPurchaseReturnRepository purchaseReturnRepo,
        IUnitOfWork uow,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _repo = repo;
        _purchaseReturnRepo = purchaseReturnRepo;
        _uow = uow;
        _t = t;
        _u = u;
    }

    public async Task<Result<IssuedWithholdingDto>> Handle(
        CancelWithholdingCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var uid = _u.UserId;

        // Fase 3 (P0-02, Remediación transaccional 02) — orden obligatorio: BeginTransaction →
        // descubrimiento (sin tracking) del PurchaseInvoiceId dueño de la retención, únicamente
        // para saber qué Lock A adquirir → Lock A → recarga autoritativa. El descubrimiento nunca
        // rastrea IssuedWithholding — la recarga posterior al lock, vía GetWithholdingByIdAsync
        // (tracking), garantiza lectura fresca real desde PostgreSQL.
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var purchaseInvoiceId = await _repo.GetWithholdingPurchaseInvoiceIdAsync(
                tid,
                cmd.WithholdingId,
                ct
            );
            if (purchaseInvoiceId is null)
            {
                await _uow.RollbackAsync(ct);
                return Result<IssuedWithholdingDto>.NotFound("Retención no encontrada.");
            }

            await _purchaseReturnRepo.AcquireFinancialLockAsync(tid, purchaseInvoiceId.Value, ct);

            // ── Recarga autoritativa de la retención — primera vez que se rastrea, ya bajo
            // el lock: garantizadamente fresca. ─────────────────────────────
            var wh = await _repo.GetWithholdingByIdAsync(tid, cmd.WithholdingId, ct);
            if (wh is null)
            {
                await _uow.RollbackAsync(ct);
                return Result<IssuedWithholdingDto>.NotFound("Retención no encontrada.");
            }

            // ── Anular en dominio (guard "Solo se pueden anular retenciones emitidas" se
            // revalida aquí sobre la instancia recargada) ────────────────────
            wh.Cancel(cmd.Reason, uid);

            // ── Revertir impacto en cuenta por pagar (recargada bajo lock) ──
            var inv = await _repo.GetByIdAsync(tid, wh.PurchaseInvoiceId, ct);
            if (inv is not null)
            {
                var payable = await _repo.GetPayableByPurchaseIdAsync(tid, inv.Id, ct);
                if (payable is not null)
                {
                    payable.ReverseRetention(inv.PaymentSchedules);
                }
            }

            // ── Persistir (auditoría de "withholding.cancelled" vía IssuedWithholdingAuditHandler,
            // disparada por el domain event levantado en wh.Cancel() dentro de este SaveChangesAsync) ──
            await _repo.SaveChangesAsync(ct);
            await _uow.CommitAsync(ct);

            return Result<IssuedWithholdingDto>.Success(MapWh.ToDto(wh));
        }
        catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
        {
            await _uow.RollbackAsync(ct);
            return Result<IssuedWithholdingDto>.Conflict(
                "La retención fue modificada por otro usuario. Recargue e intente nuevamente.",
                ApiResponseCodes.Common.ConcurrencyConflict
            );
        }
        catch (InvalidOperationException ex)
        {
            await _uow.RollbackAsync(ct);
            return Result<IssuedWithholdingDto>.ValidationFailure(ex.Message);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }
}

file static class MapWh
{
    public static IssuedWithholdingDto ToDto(IssuedWithholding w) =>
        new(
            w.Id,
            w.PurchaseInvoiceId,
            w.SupplierId,
            w.EmissionPointId,
            w.WithholdingNumber,
            w.IssueDate,
            w.AccessKey,
            w.TotalRetainedVat,
            w.TotalRetainedIncome,
            w.TotalRetainedIsd,
            w.TotalRetained,
            w.Status.ToString(),
            w.Details.Select(d => new IssuedWithholdingDetailDto(
                    d.Id,
                    d.TaxType,
                    d.RetentionCode,
                    d.RetentionCodeDescription,
                    d.TaxableBase,
                    d.RetentionPct,
                    d.AmountRetained
                ))
                .ToList(),
            w.CreatedAt,
            w.UpdatedAt
        );
}
