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
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public CancelWithholdingHandler(
        IPurchaseInvoiceRepository repo,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _repo = repo;
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

        // ── Cargar retención ────────────────────────────────────────────
        var wh = await _repo.GetWithholdingByIdAsync(tid, cmd.WithholdingId, ct);
        if (wh is null)
            return Result<IssuedWithholdingDto>.NotFound("Retención no encontrada.");

        // ── Anular en dominio ───────────────────────────────────────────
        try
        {
            wh.Cancel(cmd.Reason, uid);
        }
        catch (InvalidOperationException ex)
        {
            return Result<IssuedWithholdingDto>.ValidationFailure(ex.Message);
        }

        // ── Revertir impacto en cuenta por pagar ────────────────────────
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
        try
        {
            await _repo.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
        {
            return Result<IssuedWithholdingDto>.Conflict(
                "La retención fue modificada por otro usuario. Recargue e intente nuevamente."
            );
        }

        return Result<IssuedWithholdingDto>.Success(MapWh.ToDto(wh));
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
