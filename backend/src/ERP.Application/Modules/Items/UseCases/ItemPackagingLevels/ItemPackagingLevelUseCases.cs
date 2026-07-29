using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Items.UseCases.ItemPackagingLevels;

public record PackagingLevelInput(
    string Name,
    int Level,
    decimal BaseQuantity,
    string UomCode,
    string? Barcode = null,
    decimal? Weight = null,
    bool IsBaseUnit = false,
    bool IsPurchaseDefault = false,
    bool IsSaleDefault = false
);

public sealed record ReplaceItemPackagingLevelsCommand(
    Guid Id,
    IReadOnlyList<PackagingLevelInput> Levels)
    : IRequest<Result<ItemDetailDto>>, ICompanyScopedRequest;

public sealed class ReplaceItemPackagingLevelsCommandValidator
    : AbstractValidator<ReplaceItemPackagingLevelsCommand>
{
    public ReplaceItemPackagingLevelsCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Levels)
            .NotEmpty().WithMessage("Debe especificar al menos un nivel de empaque.")
            .Must(lvls => lvls.Count(l => l.IsBaseUnit) == 1)
            .WithMessage("Debe existir exactamente un nivel base (IsBaseUnit=true).");
        RuleForEach(x => x.Levels).ChildRules(l =>
        {
            l.RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
            l.RuleFor(x => x.UomCode).NotEmpty().MaximumLength(10);
            l.RuleFor(x => x.BaseQuantity).GreaterThan(0);
        });
    }
}

public sealed class ReplaceItemPackagingLevelsCommandHandler
    : IRequestHandler<ReplaceItemPackagingLevelsCommand, Result<ItemDetailDto>>
{
    private readonly IItemRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _user;
    private readonly ISriCatalogResolver _sri;
    private readonly IItemTypeRepository _itemTypeRepo;

    public ReplaceItemPackagingLevelsCommandHandler(
        IItemRepository repository, ICurrentTenant tenant, ICurrentUser user, ISriCatalogResolver sri,
        IItemTypeRepository itemTypeRepo)
    { _repository = repository; _currentTenant = tenant; _user = user; _sri = sri; _itemTypeRepo = itemTypeRepo; }

    public async Task<Result<ItemDetailDto>> Handle(ReplaceItemPackagingLevelsCommand cmd, CancellationToken cancellationToken)
    {
        var item = await _repository.GetByIdLightAsync(cmd.Id, _currentTenant.TenantId, cancellationToken);
        if (item is null) return Result<ItemDetailDto>.NotFound("Ítem no encontrado.");

        var newLevels = cmd.Levels
            .OrderBy(l => l.Level)
            .Select(l => ItemPackagingLevel.Create(
                cmd.Id, item.TenantId,
                l.Name, l.Level, l.BaseQuantity, l.UomCode,
                l.Barcode, l.Weight, l.IsBaseUnit, l.IsPurchaseDefault, l.IsSaleDefault, _user.UserId))
            .ToList();

        await _repository.ReplacePackagingLevelsAsync(cmd.Id, newLevels, cancellationToken);
        item.UpdateClassification(item.CategoryNodeId, item.BrandId, _user.UserId);
        await _repository.SaveChangesAsync(cancellationToken);

        var updated = await _repository.GetByIdAsync(cmd.Id, item.TenantId, cancellationToken);
        return Result<ItemDetailDto>.Success(
            await ItemMappingService.ToDetailDtoAsync(updated!, _sri, _itemTypeRepo, _currentTenant.TenantId, cancellationToken));
    }
}
