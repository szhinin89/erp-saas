using ERP.Application.Common;
using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Purchases.UseCases;

// ── Commands ────────────────────────────────────────────────────────────

public sealed record LoadPvpSnapshotsCommand(Guid InvoiceId)
    : IRequest<Result<PurchaseInvoiceDto>>,
        IBranchScopedRequest;

public sealed record UpdateLinePvpCommand(Guid InvoiceId, Guid LineId, decimal NewPvp)
    : IRequest<Result<PurchaseInvoiceDto>>,
        IBranchScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

public sealed class UpdateLinePvpValidator : AbstractValidator<UpdateLinePvpCommand>
{
    public UpdateLinePvpValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.LineId).NotEmpty();
        RuleFor(x => x.NewPvp).GreaterThanOrEqualTo(0).WithMessage("El PVP no puede ser negativo.");
    }
}

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class LoadPvpSnapshotsHandler
    : IRequestHandler<LoadPvpSnapshotsCommand, Result<PurchaseInvoiceDto>>
{
    private readonly IPurchaseInvoiceRepository _repo;
    private readonly IPricingResolver _pricingResolver;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public LoadPvpSnapshotsHandler(
        IPurchaseInvoiceRepository repo,
        IPricingResolver pricingResolver,
        ICurrentTenant t,
        ICurrentUser u
    )
    {
        _repo = repo;
        _pricingResolver = pricingResolver;
        _t = t;
        _u = u;
    }

    public async Task<Result<PurchaseInvoiceDto>> Handle(
        LoadPvpSnapshotsCommand cmd,
        CancellationToken ct
    )
    {
        var inv = await _repo.GetByIdAsync(_t.TenantId, cmd.InvoiceId, ct);
        if (inv is null)
            return Result<PurchaseInvoiceDto>.NotFound("Compra no encontrada.");
        if (inv.Status != ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Draft)
            return Result<PurchaseInvoiceDto>.ValidationFailure(
                "Solo se pueden modificar precios de venta en compras en borrador."
            );

        foreach (var line in inv.Lines)
        {
            if (line.ItemId is null)
                continue;
            var pricingResult = await _pricingResolver.ResolveAsync(line.ItemId.Value, ct: ct);
            line.SetItemPvpSnapshot(pricingResult.IsSuccess ? pricingResult.Value!.UnitPrice : 0);
        }

        await _repo.SaveChangesAsync(ct);
        return Result<PurchaseInvoiceDto>.Success(PurchaseMapper.ToDto(inv));
    }
}

public sealed class UpdateLinePvpHandler
    : IRequestHandler<UpdateLinePvpCommand, Result<PurchaseInvoiceDto>>
{
    private readonly IPurchaseInvoiceRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;

    public UpdateLinePvpHandler(IPurchaseInvoiceRepository repo, ICurrentTenant t, ICurrentUser u)
    {
        _repo = repo;
        _t = t;
        _u = u;
    }

    public async Task<Result<PurchaseInvoiceDto>> Handle(
        UpdateLinePvpCommand cmd,
        CancellationToken ct
    )
    {
        var inv = await _repo.GetByIdAsync(_t.TenantId, cmd.InvoiceId, ct);
        if (inv is null)
            return Result<PurchaseInvoiceDto>.NotFound("Compra no encontrada.");
        if (inv.Status != ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Draft)
            return Result<PurchaseInvoiceDto>.ValidationFailure(
                "Solo se pueden modificar precios de venta en compras en borrador."
            );

        if (inv.Lines.All(l => l.Id != cmd.LineId))
            return Result<PurchaseInvoiceDto>.NotFound("Línea no encontrada.");

        // Muta la línea y levanta el domain event de auditoría a través del agregado —
        // ver PurchaseInvoice.UpdateLinePvp() / PurchaseLinePvpAuditHandler.
        inv.UpdateLinePvp(cmd.LineId, cmd.NewPvp, _u.UserId);

        await _repo.SaveChangesAsync(ct);
        return Result<PurchaseInvoiceDto>.Success(PurchaseMapper.ToDto(inv));
    }
}
