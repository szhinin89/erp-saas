using ERP.Application.Common;
using ERP.Application.Modules.Companies;
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

public sealed record RecalculatePurchaseCommand(Guid InvoiceId)
    : IRequest<Result<PurchaseInvoiceDto>>,
        IBranchScopedRequest;

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

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class ApplyGlobalDiscountHandler
    : IRequestHandler<ApplyGlobalDiscountCommand, Result<PurchaseInvoiceDto>>
{
    private readonly IPurchaseInvoiceRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentBranch _b;
    private readonly ICurrentUser _u;
    private readonly ICompanyPrecisionPolicyProvider _precision;

    public ApplyGlobalDiscountHandler(
        IPurchaseInvoiceRepository repo,
        ICurrentTenant t,
        ICurrentBranch b,
        ICurrentUser u,
        ICompanyPrecisionPolicyProvider precision
    )
    {
        _precision = precision;
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
        // ERP-PRECISION-OPERATIONAL-05B: LandedUnitCost se recalcula con unitCostDecimals de la política.
        inv.ApplyUnitCostPrecision((await _precision.GetEffectiveAsync(ct)).UnitCostDecimals);
        if (inv.Lines.Count == 0)
            return Result<PurchaseInvoiceDto>.ValidationFailure("La compra no tiene líneas.");

        var blockedLine = inv.Lines.FirstOrDefault(l => l.BlocksXmlDiscount(cmd.DiscountPct));
        if (blockedLine is not null)
            return Result<PurchaseInvoiceDto>.ValidationFailure(
                $"Línea '{blockedLine.Description}': no se puede modificar el descuento del XML con ICE específico o mixto sin bases físicas verificadas.",
                "PURCHASE_ICE_XML_EDIT_BLOCKED");
        inv.ApplyGlobalDiscount(cmd.DiscountPct, _u.UserId);

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
    private readonly ICompanyPrecisionPolicyProvider _precision;

    public RecalculatePurchaseHandler(
        IPurchaseInvoiceRepository repo,
        ISriTaxResolver tax,
        ICurrentTenant t,
        ICurrentBranch b,
        ICurrentUser u,
        ICompanyPrecisionPolicyProvider precision
    )
    {
        _precision = precision;
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
        // ERP-PRECISION-OPERATIONAL-05B: LandedUnitCost se recalcula con unitCostDecimals de la política.
        inv.ApplyUnitCostPrecision((await _precision.GetEffectiveAsync(ct)).UnitCostDecimals);
        if (inv.Status != ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Draft)
            return Result<PurchaseInvoiceDto>.ValidationFailure(
                "Solo se puede recalcular una compra en estado borrador."
            );
        if (inv.Lines.Count == 0)
            return Result<PurchaseInvoiceDto>.ValidationFailure("La compra no tiene líneas.");

        foreach (var line in inv.Lines)
        {
            var taxError = await PurchaseTaxHelper.ResolveTaxesAsync(line, _tax, ct);
            if (taxError is not null)
                return taxError;
        }

        inv.RecalculateCostDistributions(_u.UserId);

        await _repo.SaveChangesAsync(ct);
        return Result<PurchaseInvoiceDto>.Success(PurchaseMapper.ToDto(inv));
    }
}
