using FluentValidation;
using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchases;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Application.Modules.Purchases.Services;

namespace ERP.Application.Modules.Purchases.UseCases;

// ── Commands ────────────────────────────────────────────────────────────

public sealed record ApplyGlobalDiscountCommand(Guid InvoiceId, decimal DiscountPct)
    : IRequest<Result<PurchaseInvoiceDto>>, IBranchScopedRequest;

public sealed record AllocateFreightCommand(Guid InvoiceId)
    : IRequest<Result<PurchaseInvoiceDto>>, IBranchScopedRequest;

public sealed record RecalculatePurchaseCommand(Guid InvoiceId)
    : IRequest<Result<PurchaseInvoiceDto>>, IBranchScopedRequest;

// ── Validators ──────────────────────────────────────────────────────────

public sealed class ApplyGlobalDiscountValidator : AbstractValidator<ApplyGlobalDiscountCommand>
{
    public ApplyGlobalDiscountValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.DiscountPct).InclusiveBetween(0, 100)
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

// ── Handlers ────────────────────────────────────────────────────────────

public sealed class ApplyGlobalDiscountHandler
    : IRequestHandler<ApplyGlobalDiscountCommand, Result<PurchaseInvoiceDto>>
{
    private readonly IPurchaseInvoiceRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentUser _u;
    public ApplyGlobalDiscountHandler(IPurchaseInvoiceRepository repo, ICurrentTenant t, ICurrentUser u)
    { _repo = repo; _t = t; _u = u; }

    public async Task<Result<PurchaseInvoiceDto>> Handle(ApplyGlobalDiscountCommand cmd, CancellationToken ct)
    {
        var inv = await _repo.GetByIdAsync(_t.TenantId, cmd.InvoiceId, ct);
        if (inv is null) return Result<PurchaseInvoiceDto>.NotFound("Compra no encontrada.");
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
    private readonly ICurrentUser _u;
    public AllocateFreightHandler(IPurchaseInvoiceRepository repo, ICurrentTenant t, ICurrentUser u)
    { _repo = repo; _t = t; _u = u; }

    public async Task<Result<PurchaseInvoiceDto>> Handle(AllocateFreightCommand cmd, CancellationToken ct)
    {
        var inv = await _repo.GetByIdAsync(_t.TenantId, cmd.InvoiceId, ct);
        if (inv is null) return Result<PurchaseInvoiceDto>.NotFound("Compra no encontrada.");
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
    private readonly ICurrentUser _u;
    public RecalculatePurchaseHandler(IPurchaseInvoiceRepository repo, ISriTaxResolver tax, ICurrentTenant t, ICurrentUser u)
    { _repo = repo; _tax = tax; _t = t; _u = u; }

    public async Task<Result<PurchaseInvoiceDto>> Handle(RecalculatePurchaseCommand cmd, CancellationToken ct)
    {
        var inv = await _repo.GetByIdAsync(_t.TenantId, cmd.InvoiceId, ct);
        if (inv is null) return Result<PurchaseInvoiceDto>.NotFound("Compra no encontrada.");
        if (inv.Status != ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Draft)
            return Result<PurchaseInvoiceDto>.ValidationFailure("Solo se puede recalcular una compra en estado borrador.");
        if (inv.Lines.Count == 0)
            return Result<PurchaseInvoiceDto>.ValidationFailure("La compra no tiene líneas.");

        try
        {
            foreach (var line in inv.Lines)
            {
                var vatResult = await _tax.GetVatRateWithNameAsync(line.VatCode, ct);
                if (vatResult is null) return Result<PurchaseInvoiceDto>.ValidationFailure($"Código IVA '{line.VatCode}' no encontrado.");

                decimal iceRate = 0;
                string? iceName = null;
                if (!string.IsNullOrWhiteSpace(line.IceCode))
                {
                    var iceResult = await _tax.GetIceRateWithNameAsync(line.IceCode, ct);
                    if (iceResult is null) return Result<PurchaseInvoiceDto>.ValidationFailure($"Código ICE '{line.IceCode}' no encontrado.");
                    iceRate = iceResult.Rate;
                    iceName = iceResult.Name;
                }

                line.ApplyTaxes(line.VatCode, vatResult.Rate, vatResult.Name,
                                line.IceCode, iceRate, iceName);
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

