using ERP.Application.Common;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Purchases.UseCases;

// ── Command ─────────────────────────────────────────────────────────────

public sealed record CancelPurchaseCommand(Guid PurchaseInvoiceId, string Reason)
    : IRequest<Result<PurchaseInvoiceDto>>,
        IBranchScopedRequest;

// ── Validator ───────────────────────────────────────────────────────────

public sealed class CancelPurchaseValidator : AbstractValidator<CancelPurchaseCommand>
{
    public CancelPurchaseValidator()
    {
        RuleFor(x => x.PurchaseInvoiceId).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(PurchaseInvoice.CancelReasonMaxLen)
            .WithMessage("El motivo de anulación es obligatorio (máximo 500 caracteres).");
    }
}

// ── Handler ─────────────────────────────────────────────────────────────

public sealed class CancelPurchaseHandler
    : IRequestHandler<CancelPurchaseCommand, Result<PurchaseInvoiceDto>>
{
    private readonly IPurchaseInvoiceRepository _repo;
    private readonly IStockRepository _stockRepo;
    private readonly ILogger<CancelPurchaseHandler> _logger;
    private readonly ICurrentTenant _t;
    private readonly ICurrentCompany _c;
    private readonly ICurrentUser _u;

    public CancelPurchaseHandler(
        IPurchaseInvoiceRepository repo,
        IStockRepository stockRepo,
        ILogger<CancelPurchaseHandler> logger,
        ICurrentTenant t,
        ICurrentCompany c,
        ICurrentUser u
    )
    {
        _repo = repo;
        _stockRepo = stockRepo;
        _logger = logger;
        _t = t;
        _c = c;
        _u = u;
    }

    public async Task<Result<PurchaseInvoiceDto>> Handle(
        CancelPurchaseCommand cmd,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;
        var cid = _c.CompanyId;
        var uid = _u.UserId;

        // ── 1. Cargar y validar ─────────────────────────────────────────
        var inv = await _repo.GetByIdAsync(tid, cmd.PurchaseInvoiceId, ct);
        if (inv is null)
            return Result<PurchaseInvoiceDto>.NotFound("Compra no encontrada.");

        if (inv.Status == ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Cancelled)
            return Result<PurchaseInvoiceDto>.ValidationFailure("Esta compra ya fue anulada.");

        _logger.LogInformation(
            "Cancelling purchase {InvoiceNumber} ({InvoiceId}) for tenant {TenantId}. Reason: {Reason}",
            inv.InvoiceNumber,
            inv.Id,
            tid,
            cmd.Reason
        );

        // ── 1b. Cargar cuenta por pagar y bloquear si hay pagos ─────────
        var payable = await _repo.GetPayableByPurchaseIdAsync(tid, inv.Id, ct);
        if (payable is not null && payable.PaidAmount > 0)
            return Result<PurchaseInvoiceDto>.ValidationFailure(
                "No se puede anular una compra con pagos aplicados. Reverse los pagos primero."
            );

        // ── 2. Anular retención (si existe) ─────────────────────────────
        var wh = await _repo.GetWithholdingByPurchaseIdAsync(tid, inv.Id, ct);
        if (
            wh is not null
            && wh.Status == ERP.Domain.Modules.Purchases.Enums.WithholdingStatus.Issued
        )
        {
            wh.Cancel("Anulación automática por anulación de compra.", uid);

            if (payable is not null)
                payable.ReverseRetention(inv.PaymentSchedules);
        }
        if (payable is not null)
        {
            try
            {
                payable.CancelPayable();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(
                    "Cannot cancel payable for invoice {InvoiceId}: {Reason}",
                    inv.Id,
                    ex.Message
                );
                return Result<PurchaseInvoiceDto>.ValidationFailure(ex.Message);
            }
        }

        // ── 4. Revertir stock ───────────────────────────────────────────
        foreach (var line in inv.Lines)
        {
            if (line.ItemId is null)
                continue;
            var warehouseId = line.WarehouseId ?? inv.GlobalWarehouseId;
            if (warehouseId is null)
                continue;

            await _stockRepo.AppendMovementAsync(
                tid,
                cid,
                line.ItemId.Value,
                warehouseId.Value,
                StockMovementType.PurchaseReturn,
                -line.Quantity,
                line.UomCode,
                DateOnly.FromDateTime(DateTime.UtcNow),
                $"ANULACIÓN: {inv.InvoiceNumber}",
                inv.Id,
                "PurchaseInvoice",
                uid,
                line.LandedUnitCost,
                cancellationToken: ct
            );
        }

        // ── 5. Cambiar estado compra ────────────────────────────────────
        try
        {
            inv.Cancel(cmd.Reason, uid);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                "Cancel rejected for invoice {InvoiceId}: {Reason}",
                inv.Id,
                ex.Message
            );
            return Result<PurchaseInvoiceDto>.ValidationFailure(ex.Message);
        }

        // ── 6. Persistir (transacción atómica vía EF SaveChanges) ───────
        // La auditoría de "purchase.cancelled" (y la de la retención anulada en cascada,
        // si existía) se registra automáticamente vía *AuditHandler, disparada por los
        // domain events levantados en inv.Cancel() / wh.Cancel() dentro de este SaveChangesAsync.
        await _stockRepo.SaveChangesWithSequenceRetryAsync(ct);

        _logger.LogInformation(
            "Purchase {InvoiceNumber} ({InvoiceId}) cancelled successfully",
            inv.InvoiceNumber,
            inv.Id
        );

        return Result<PurchaseInvoiceDto>.Success(PurchaseMapper.ToDto(inv));
    }
}
