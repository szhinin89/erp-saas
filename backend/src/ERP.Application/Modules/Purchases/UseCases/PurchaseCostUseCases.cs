using ERP.Application.Common;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Application.Modules.Purchases.Services;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Purchases.UseCases;

// ── Commands ────────────────────────────────────────────────────────────

public sealed record ApplyGlobalDiscountCommand(Guid InvoiceId, decimal DiscountPct)
    : IRequest<Result<PurchaseInvoiceDto>>,
        IBranchScopedRequest;

public sealed record AllocateFreightCommand(Guid InvoiceId)
    : IRequest<Result<PurchaseInvoiceDto>>,
        IBranchScopedRequest;

public sealed record RecalculatePurchaseCommand(Guid InvoiceId)
    : IRequest<Result<PurchaseInvoiceDto>>,
        IBranchScopedRequest;

/// <summary>
/// PURCHASE-FREIGHT-DISTRIBUTION-MODAL-01 — aplica el prorrateo aditivo revisado por el usuario en
/// el modal "Distribuir flete/gasto". A diferencia de <see cref="AllocateFreightCommand"/>
/// (redistribuye el total ya persistido entre TODAS las líneas), este comando suma un monto nuevo
/// únicamente entre <c>IncludedLineIds</c>.
/// PURCHASE-COSTTYPE-ENUM-CONTRACT-CLEANUP-01 — <c>CostType</c> es <see cref="PurchaseCostType"/>
/// (antes <c>string</c> "Freight"/"OtherCost" re-validado a mano). El payload HTTP no cambió: sigue
/// llegando como string ("Freight"/"OtherCost") en <c>DistributeCostRequest</c>, y
/// <c>PurchasesController.DistributeCost</c> lo convierte con <c>Enum.TryParse</c> antes de construir
/// este comando — un valor inválido nunca llega hasta acá.
/// </summary>
public sealed record DistributePurchaseCostCommand(
    Guid InvoiceId,
    PurchaseCostType CostType,
    decimal Amount,
    List<Guid> IncludedLineIds
) : IRequest<Result<PurchaseInvoiceDto>>, IBranchScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

public sealed class ApplyGlobalDiscountValidator : AbstractValidator<ApplyGlobalDiscountCommand>
{
    public ApplyGlobalDiscountValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.DiscountPct)
            .InclusiveBetween(0, 100)
            .WithMessage("El descuento debe estar entre 0% y 100%.");
    }
}

public sealed class AllocateFreightValidator : AbstractValidator<AllocateFreightCommand>
{
    public AllocateFreightValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty();
    }
}

public sealed class DistributePurchaseCostValidator
    : AbstractValidator<DistributePurchaseCostCommand>
{
    public DistributePurchaseCostValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty();
        // PURCHASE-COSTTYPE-ENUM-CONTRACT-CLEANUP-01 — el string original ("Freight"/"OtherCost")
        // ya se validó y convirtió en PurchasesController.DistributeCost (Enum.TryParse, mensaje
        // amigable si falla); acá solo queda el guard defensivo de que el enum llegue en rango.
        RuleFor(x => x.CostType).IsInEnum();
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("El valor a distribuir debe ser mayor a cero.");
        RuleFor(x => x.IncludedLineIds)
            .NotEmpty()
            .WithMessage("Debe incluir al menos una línea.");
    }
}

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class ApplyGlobalDiscountHandler
    : IRequestHandler<ApplyGlobalDiscountCommand, Result<PurchaseInvoiceDto>>
{
    private readonly IPurchaseInvoiceRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;

    public ApplyGlobalDiscountHandler(
        IPurchaseInvoiceRepository repo,
        ICurrentTenant t,
        ICurrentBranch b,
        ICurrentUser u
    )
    {
        _repo = repo;
        _t = t;
        _b = b;
        _u = u;
    }

    public async Task<Result<PurchaseInvoiceDto>> Handle(
        ApplyGlobalDiscountCommand cmd,
        CancellationToken ct
    )
    {
        var inv = await _repo.GetByIdAsync(_t.TenantId, cmd.InvoiceId, ct);
        if (inv is null || inv.BranchId != _b.BranchId)
            return Result<PurchaseInvoiceDto>.NotFound("Compra no encontrada.");
        if (inv.Lines.Count == 0)
            return Result<PurchaseInvoiceDto>.ValidationFailure("La compra no tiene líneas.");

        try
        {
            inv.ApplyGlobalDiscount(cmd.DiscountPct, _u.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<PurchaseInvoiceDto>.ValidationFailure(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        return Result<PurchaseInvoiceDto>.Success(PurchaseMapper.ToDto(inv));
    }
}

public sealed class AllocateFreightHandler
    : IRequestHandler<AllocateFreightCommand, Result<PurchaseInvoiceDto>>
{
    private readonly IPurchaseInvoiceRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;

    public AllocateFreightHandler(
        IPurchaseInvoiceRepository repo,
        ICurrentTenant t,
        ICurrentBranch b,
        ICurrentUser u
    )
    {
        _repo = repo;
        _t = t;
        _b = b;
        _u = u;
    }

    public async Task<Result<PurchaseInvoiceDto>> Handle(
        AllocateFreightCommand cmd,
        CancellationToken ct
    )
    {
        var inv = await _repo.GetByIdAsync(_t.TenantId, cmd.InvoiceId, ct);
        if (inv is null || inv.BranchId != _b.BranchId)
            return Result<PurchaseInvoiceDto>.NotFound("Compra no encontrada.");
        if (inv.Lines.Count == 0)
            return Result<PurchaseInvoiceDto>.ValidationFailure("La compra no tiene líneas.");

        try
        {
            inv.DistributeCosts(inv.TotalFreight, inv.TotalOtherCosts, _u.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<PurchaseInvoiceDto>.ValidationFailure(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        return Result<PurchaseInvoiceDto>.Success(PurchaseMapper.ToDto(inv));
    }
}

public sealed class RecalculatePurchaseHandler
    : IRequestHandler<RecalculatePurchaseCommand, Result<PurchaseInvoiceDto>>
{
    private readonly IPurchaseInvoiceRepository _repo;
    private readonly ISriTaxResolver _tax;
    private readonly ICurrentTenant _t;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;

    public RecalculatePurchaseHandler(
        IPurchaseInvoiceRepository repo,
        ISriTaxResolver tax,
        ICurrentTenant t,
        ICurrentBranch b,
        ICurrentUser u
    )
    {
        _repo = repo;
        _tax = tax;
        _t = t;
        _b = b;
        _u = u;
    }

    public async Task<Result<PurchaseInvoiceDto>> Handle(
        RecalculatePurchaseCommand cmd,
        CancellationToken ct
    )
    {
        var inv = await _repo.GetByIdAsync(_t.TenantId, cmd.InvoiceId, ct);
        if (inv is null || inv.BranchId != _b.BranchId)
            return Result<PurchaseInvoiceDto>.NotFound("Compra no encontrada.");
        if (inv.Status != ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Draft)
            return Result<PurchaseInvoiceDto>.ValidationFailure(
                "Solo se puede recalcular una compra en estado borrador."
            );
        if (inv.Lines.Count == 0)
            return Result<PurchaseInvoiceDto>.ValidationFailure("La compra no tiene líneas.");

        try
        {
            foreach (var line in inv.Lines)
            {
                var vatResult = await _tax.GetVatRateWithNameAsync(line.VatCode, ct);
                if (vatResult is null)
                    return Result<PurchaseInvoiceDto>.ValidationFailure(
                        $"Código IVA '{line.VatCode}' no encontrado."
                    );

                decimal iceRate = 0;
                string? iceName = null;
                var iceCalculationType = ERP.Domain.Modules.SriCatalogs.Enums.SriTaxCalculationType.Percentage;
                decimal? iceExactAmount = null;
                if (!string.IsNullOrWhiteSpace(line.IceCode))
                {
                    // FLOW-READY-02F.1 — catalog-aware (no el legacy GetIceRateWithNameAsync, que exige
                    // Percentage y por eso nunca resuelve ICE "específico" como el código 3053). Mismo
                    // criterio que ConfirmPurchaseUseCases.
                    var iceEntry = await _tax.GetIceCatalogEntryAsync(line.IceCode, ct);
                    if (iceEntry is null)
                        return Result<PurchaseInvoiceDto>.ValidationFailure(
                            $"Código ICE '{line.IceCode}' no encontrado."
                        );
                    iceName = iceEntry.Name;
                    iceCalculationType = iceEntry.CalculationType;
                    if (iceEntry.CalculationType
                        == ERP.Domain.Modules.SriCatalogs.Enums.SriTaxCalculationType.Specific)
                    {
                        // El monto ya fue fijado al valor exacto (XML o catálogo) al crear/actualizar
                        // la línea — Recalculate lo preserva, igual que Confirm, nunca lo recalcula
                        // desde una tarifa porcentual.
                        iceExactAmount = line.IceAmount;
                    }
                    else
                    {
                        iceRate = iceEntry.Percentage ?? 0m;
                    }
                }

                line.ApplyTaxes(
                    line.VatCode,
                    vatResult.Rate,
                    vatResult.Name,
                    line.IceCode,
                    iceRate,
                    iceName,
                    iceCalculationType,
                    iceExactAmount
                );
            }

            inv.DistributeCosts(inv.TotalFreight, inv.TotalOtherCosts, _u.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<PurchaseInvoiceDto>.ValidationFailure(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        return Result<PurchaseInvoiceDto>.Success(PurchaseMapper.ToDto(inv));
    }
}

public sealed class DistributePurchaseCostHandler
    : IRequestHandler<DistributePurchaseCostCommand, Result<PurchaseInvoiceDto>>
{
    private readonly IPurchaseInvoiceRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;

    public DistributePurchaseCostHandler(
        IPurchaseInvoiceRepository repo,
        ICurrentTenant t,
        ICurrentBranch b,
        ICurrentUser u
    )
    {
        _repo = repo;
        _t = t;
        _b = b;
        _u = u;
    }

    public async Task<Result<PurchaseInvoiceDto>> Handle(
        DistributePurchaseCostCommand cmd,
        CancellationToken ct
    )
    {
        var inv = await _repo.GetByIdAsync(_t.TenantId, cmd.InvoiceId, ct);
        if (inv is null || inv.BranchId != _b.BranchId)
            return Result<PurchaseInvoiceDto>.NotFound("Compra no encontrada.");
        if (inv.Lines.Count == 0)
            return Result<PurchaseInvoiceDto>.ValidationFailure("La compra no tiene líneas.");

        try
        {
            inv.DistributeAdditionalCost(cmd.CostType, cmd.Amount, cmd.IncludedLineIds, _u.UserId);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result<PurchaseInvoiceDto>.ValidationFailure(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
        return Result<PurchaseInvoiceDto>.Success(PurchaseMapper.ToDto(inv));
    }
}
