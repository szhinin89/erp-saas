using ERP.Application.Common;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Purchases.UseCases;

// PURCHASE-COST-DISTRIBUTION-CANONICAL-01 — única operación pública para crear, reemplazar o retirar
// una distribución de costos de un borrador (flete/otros costos externos o transporte XML).

public sealed record ReplacePurchaseCostDistributionCommand(
    Guid InvoiceId,
    Guid DistributionId,
    PurchaseCostType CostType,
    long ExpectedRevision,
    decimal? Amount,
    List<Guid> TargetLineIds,
    Guid? SourceLineId
) : IRequest<Result<PurchaseInvoiceDto>>, IBranchScopedRequest;

public sealed record RemovePurchaseCostDistributionCommand(
    Guid InvoiceId,
    Guid DistributionId,
    long ExpectedRevision
) : IRequest<Result<PurchaseInvoiceDto>>, IBranchScopedRequest;

public sealed class ReplacePurchaseCostDistributionValidator
    : AbstractValidator<ReplacePurchaseCostDistributionCommand>
{
    public ReplacePurchaseCostDistributionValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.DistributionId).NotEmpty();
        RuleFor(x => x.CostType).IsInEnum();
        RuleFor(x => x.ExpectedRevision).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TargetLineIds).NotEmpty().WithMessage("Debe incluir al menos una línea.");
        RuleFor(x => x.Amount)
            .NotNull()
            .GreaterThan(0)
            .When(x => x.SourceLineId is null)
            .WithMessage("El valor a distribuir debe ser mayor a cero.");
        RuleFor(x => x.Amount)
            .Null()
            .When(x => x.SourceLineId is not null)
            .WithMessage("El importe del transporte XML se toma de su línea fiscal.");
    }
}

public sealed class RemovePurchaseCostDistributionValidator
    : AbstractValidator<RemovePurchaseCostDistributionCommand>
{
    public RemovePurchaseCostDistributionValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.DistributionId).NotEmpty();
        RuleFor(x => x.ExpectedRevision).GreaterThan(0);
    }
}

public sealed class ReplacePurchaseCostDistributionHandler(
    IPurchaseInvoiceRepository repo,
    IItemRepository items,
    ICurrentTenant tenant,
    ICurrentBranch branch,
    ICurrentUser user,
    ICompanyPrecisionPolicyProvider precision
) : IRequestHandler<ReplacePurchaseCostDistributionCommand, Result<PurchaseInvoiceDto>>
{
    public async Task<Result<PurchaseInvoiceDto>> Handle(
        ReplacePurchaseCostDistributionCommand cmd,
        CancellationToken ct
    )
    {
        var inv = await repo.GetByIdAsync(tenant.TenantId, cmd.InvoiceId, ct);
        if (inv is null || inv.BranchId != branch.BranchId)
            return Result<PurchaseInvoiceDto>.NotFound("Compra no encontrada.");
        inv.ApplyUnitCostPrecision((await precision.GetEffectiveAsync(ct)).UnitCostDecimals);
        var natures = new Dictionary<Guid, ItemNature>();
        foreach (var itemId in inv.Lines.Where(l => l.ItemId.HasValue).Select(l => l.ItemId!.Value).Distinct())
        {
            var item = await items.GetByIdLightAsync(itemId, tenant.TenantId, ct);
            if (item is null || item.CompanyId != inv.CompanyId)
                return Result<PurchaseInvoiceDto>.ValidationFailure("El ítem no pertenece a esta empresa.");
            natures[itemId] = item.Nature;
        }
        inv.ReplaceCostDistribution(
            new PurchaseCostDistributionChange(cmd.DistributionId, cmd.ExpectedRevision, cmd.CostType,
                cmd.Amount, cmd.TargetLineIds, cmd.SourceLineId),
            natures,
            user.UserId
        );
        return await PurchaseCostDistributionPersistence.SaveAsync(repo, inv, ct);
    }
}

public sealed class RemovePurchaseCostDistributionHandler(
    IPurchaseInvoiceRepository repo,
    ICurrentTenant tenant,
    ICurrentBranch branch,
    ICurrentUser user,
    ICompanyPrecisionPolicyProvider precision
) : IRequestHandler<RemovePurchaseCostDistributionCommand, Result<PurchaseInvoiceDto>>
{
    public async Task<Result<PurchaseInvoiceDto>> Handle(
        RemovePurchaseCostDistributionCommand cmd,
        CancellationToken ct
    )
    {
        var inv = await repo.GetByIdAsync(tenant.TenantId, cmd.InvoiceId, ct);
        if (inv is null || inv.BranchId != branch.BranchId)
            return Result<PurchaseInvoiceDto>.NotFound("Compra no encontrada.");
        inv.ApplyUnitCostPrecision((await precision.GetEffectiveAsync(ct)).UnitCostDecimals);
        inv.RemoveCostDistribution(cmd.DistributionId, cmd.ExpectedRevision, user.UserId);
        return await PurchaseCostDistributionPersistence.SaveAsync(repo, inv, ct);
    }
}

internal static class PurchaseCostDistributionPersistence
{
    /// <summary>
    /// Un cambio concurrente (xmin de la compra o revisión de la distribución) se rechaza sin
    /// efectos: el cliente debe recargar y volver a decidir.
    /// </summary>
    public static async Task<Result<PurchaseInvoiceDto>> SaveAsync(
        IPurchaseInvoiceRepository repo,
        PurchaseInvoice inv,
        CancellationToken ct
    )
    {
        try
        {
            await repo.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
        {
            return Result<PurchaseInvoiceDto>.Conflict(
                "La compra fue modificada por otro usuario. Recargue antes de distribuir costos."
            );
        }
        return Result<PurchaseInvoiceDto>.Success(PurchaseMapper.ToDto(inv));
    }
}
