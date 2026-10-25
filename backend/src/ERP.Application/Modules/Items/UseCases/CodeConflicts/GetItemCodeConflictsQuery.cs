using ERP.Application.Common;
using ERP.Application.Items.UseCases.AddItemVariant;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.Models;
using FluentValidation;
using MediatR;

namespace ERP.Application.Items.UseCases.CodeConflicts;

public sealed record ItemCodeCandidate(string Kind, string Code, string Row, string TargetKey,
    Guid? SupplierId = null, Guid? ExcludeId = null, AddItemVariantCommand? Variant = null);

public sealed record ItemCodeConflictDto(string Kind, string Code, Guid? ItemId, string? ItemName,
    IReadOnlyList<string> Rows);

/// <summary>Read-only diagnostics shared by all Item editors. Persistence remains authoritative.</summary>
public sealed record GetItemCodeConflictsQuery(IReadOnlyList<ItemCodeCandidate> Codes)
    : IRequest<Result<IReadOnlyList<ItemCodeConflictDto>>>, ICompanyScopedRequest;

public sealed class GetItemCodeConflictsQueryValidator : AbstractValidator<GetItemCodeConflictsQuery>
{
    public GetItemCodeConflictsQueryValidator()
    {
        RuleFor(q => q.Codes).NotNull().Must(c => c is not null && c.Count <= 500);
        RuleForEach(q => q.Codes).ChildRules(c =>
        {
            c.RuleFor(x => x.Kind).Must(k => k is "Sku" or "VariantSku" or "Barcode" or "SupplierCode");
            c.RuleFor(x => x.Code).MaximumLength(100);
            c.RuleFor(x => x.Code).NotEmpty().When(x => x.Variant is null);
            c.RuleFor(x => x.Variant!).SetValidator(new AddItemVariantCommandValidator()).When(x => x.Variant is not null);
            c.RuleFor(x => x.Row).NotEmpty().MaximumLength(100);
            c.RuleFor(x => x.TargetKey).NotEmpty().MaximumLength(150);
            c.RuleFor(x => x.SupplierId).NotEmpty().When(x => x.Kind == "SupplierCode");
        });
    }
}

public sealed class GetItemCodeConflictsQueryHandler(IItemRepository items, ICurrentTenant tenant)
    : IRequestHandler<GetItemCodeConflictsQuery, Result<IReadOnlyList<ItemCodeConflictDto>>>
{
    public async Task<Result<IReadOnlyList<ItemCodeConflictDto>>> Handle(GetItemCodeConflictsQuery request,
        CancellationToken cancellationToken)
    {
        var result = new List<ItemCodeConflictDto>();
        var resolved = new List<ItemCodeCandidate>();
        foreach (var candidate in request.Codes)
        {
            if (candidate.Kind == "VariantSku" && candidate.Variant is { } variant)
            {
                var item = await items.GetByIdLightAsync(variant.ItemId, tenant.TenantId, cancellationToken);
                if (item is null) continue;
                resolved.Add(candidate with { Code = item.ResolveVariantSku(
                    variant.Attributes.Select(a => (a.AttributeDefinitionId, a.Value)).ToList(), variant.SkuOverride) });
            }
            else resolved.Add(candidate);
        }
        var groups = resolved.GroupBy(c => (c.Kind,
            Code: c.Kind is "Sku" or "VariantSku" ? c.Code.Trim().ToUpperInvariant() : c.Code.Trim(),
            Supplier: c.Kind == "SupplierCode" ? c.SupplierId : null));
        foreach (var group in groups)
        {
            var rows = group.Select(c => c.Row).Distinct().ToList();
            if (group.Select(c => c.TargetKey).Distinct().Skip(1).Any())
                result.Add(new(group.Key.Kind, group.Key.Code, null, null, rows));
            var owners = new HashSet<Guid>();
            foreach (var candidate in group.DistinctBy(c => c.ExcludeId))
            {
                var owner = await items.FindCodeOwnerAsync(new(group.Key.Kind, group.Key.Code,
                    group.Key.Supplier, candidate.ExcludeId), tenant.TenantId, cancellationToken);
                if (owner is not null && owners.Add(owner.ItemId))
                    result.Add(new(group.Key.Kind, group.Key.Code, owner.ItemId, owner.ItemName, rows));
            }
        }
        return Result<IReadOnlyList<ItemCodeConflictDto>>.Success(result);
    }
}
