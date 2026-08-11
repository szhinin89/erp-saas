using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Items.UseCases.ItemPackagingLevels;

public record PackagingLevelInput(
    Guid? Id,
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
    IReadOnlyList<PackagingLevelInput> Levels
) : IRequest<Result<ItemDetailDto>>, ICompanyScopedRequest;

public sealed class ReplaceItemPackagingLevelsCommandValidator
    : AbstractValidator<ReplaceItemPackagingLevelsCommand>
{
    public ReplaceItemPackagingLevelsCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Levels)
            .NotNull()
            .WithMessage("Debe enviar la lista de niveles de empaque.")
            .Must(lvls =>
                lvls is not null
                && lvls.All(l => !string.IsNullOrWhiteSpace(l.UomCode))
                && lvls.Select(l => new
                    {
                        UomCode = l.UomCode.Trim().ToUpperInvariant(),
                        l.BaseQuantity,
                    })
                    .Distinct()
                    .Count() == lvls.Count
            )
            .WithMessage("No se puede duplicar UOM y cantidad base en empaques.");
        RuleForEach(x => x.Levels)
            .ChildRules(l =>
            {
                l.RuleFor(x => x.Id).NotEqual(Guid.Empty).When(x => x.Id.HasValue);
                l.RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
                l.RuleFor(x => x.UomCode).NotEmpty().MaximumLength(10);
                l.RuleFor(x => x.BaseQuantity).GreaterThan(0);
                l.RuleFor(x => x.BaseQuantity)
                    .Equal(1m)
                    .When(x => x.IsBaseUnit)
                    .WithMessage("La presentación base debe tener cantidad base 1.");
            });
    }
}

public sealed class ReplaceItemPackagingLevelsCommandHandler
    : IRequestHandler<ReplaceItemPackagingLevelsCommand, Result<ItemDetailDto>>
{
    private readonly IItemRepository _repository;
    private readonly IPurchaseInvoiceRepository _purchaseRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _user;
    private readonly ISriCatalogResolver _sri;
    private readonly IItemTypeRepository _itemTypeRepo;

    public ReplaceItemPackagingLevelsCommandHandler(
        IItemRepository repository,
        IPurchaseInvoiceRepository purchaseRepository,
        ICurrentTenant tenant,
        ICurrentUser user,
        ISriCatalogResolver sri,
        IItemTypeRepository itemTypeRepo
    )
    {
        _repository = repository;
        _purchaseRepository = purchaseRepository;
        _currentTenant = tenant;
        _user = user;
        _sri = sri;
        _itemTypeRepo = itemTypeRepo;
    }

    public async Task<Result<ItemDetailDto>> Handle(
        ReplaceItemPackagingLevelsCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var item = await _repository.GetByIdAsync(
            cmd.Id,
            _currentTenant.TenantId,
            cancellationToken
        );
        if (item is null)
            return Result<ItemDetailDto>.NotFound("Ítem no encontrado.");

        var levels = cmd.Levels ?? [];
        var baseCount = levels.Count(l => l.IsBaseUnit);
        if (item.StockConfig.TracksStock && baseCount != 1)
        {
            return Result<ItemDetailDto>.ValidationFailure(
                "Los ítems que manejan stock deben tener exactamente una presentación base."
            );
        }
        if (!item.StockConfig.TracksStock && baseCount > 1)
        {
            return Result<ItemDetailDto>.ValidationFailure(
                "No puede existir más de una presentación marcada como unidad base."
            );
        }
        if (levels.Any(l => l.IsBaseUnit && l.BaseQuantity != 1m))
        {
            return Result<ItemDetailDto>.ValidationFailure(
                "La presentación base debe tener cantidad base 1."
            );
        }

        var currentPackagingIds = item.PackagingLevels.Select(p => p.Id).ToHashSet();
        var foreignId = levels.FirstOrDefault(l =>
            l.Id.HasValue && !currentPackagingIds.Contains(l.Id.Value)
        );
        if (foreignId is not null)
        {
            return Result<ItemDetailDto>.ValidationFailure(
                "El nivel de empaque no pertenece al ítem."
            );
        }

        var submittedIds = levels.Where(l => l.Id.HasValue).Select(l => l.Id!.Value).ToHashSet();
        var removedUsedLevel = item.SupplierCodes.Any(s =>
            s.IsActive && s.PackagingLevelId.HasValue && !submittedIds.Contains(s.PackagingLevelId.Value)
        );
        if (removedUsedLevel)
        {
            return Result<ItemDetailDto>.ValidationFailure(
                "No se puede quitar una presentación usada por códigos de proveedor."
            );
        }

        var usedInConfirmedDocuments =
            await _purchaseRepository.GetPackagingLevelIdsUsedInConfirmedDocumentsAsync(
                _currentTenant.TenantId,
                item.Id,
                currentPackagingIds,
                cancellationToken
            );
        if (usedInConfirmedDocuments.Count > 0)
        {
            var removedDocumentLevel = usedInConfirmedDocuments.Any(id => !submittedIds.Contains(id));
            if (removedDocumentLevel)
            {
                return Result<ItemDetailDto>.ValidationFailure(
                    "No se puede quitar una presentación usada por documentos confirmados."
                );
            }

            var currentById = item.PackagingLevels.ToDictionary(p => p.Id);
            var changedDocumentLevel = levels.Any(l =>
                l.Id.HasValue
                && usedInConfirmedDocuments.Contains(l.Id.Value)
                && currentById.TryGetValue(l.Id.Value, out var current)
                && current.BaseQuantity != l.BaseQuantity
            );
            if (changedDocumentLevel)
            {
                return Result<ItemDetailDto>.ValidationFailure(
                    "No se puede cambiar la cantidad base de una presentación usada en documentos confirmados. Cree una nueva presentación."
                );
            }
        }

        var newLevels = levels
            .OrderBy(l => l.Level)
            .Select(l =>
                ItemPackagingLevel.Create(
                    cmd.Id,
                    item.TenantId,
                    l.Id,
                    l.Name,
                    l.Level,
                    l.BaseQuantity,
                    l.UomCode,
                    l.Barcode,
                    l.Weight,
                    l.IsBaseUnit,
                    l.IsPurchaseDefault,
                    l.IsSaleDefault,
                    _user.UserId
                )
            )
            .ToList();

        await _repository.ReplacePackagingLevelsAsync(cmd.Id, newLevels, cancellationToken);
        item.UpdateClassification(item.CategoryNodeId, item.BrandId, _user.UserId);
        await _repository.SaveChangesAsync(cancellationToken);

        var updated = await _repository.GetByIdAsync(cmd.Id, item.TenantId, cancellationToken);
        return Result<ItemDetailDto>.Success(
            await ItemMappingService.ToDetailDtoAsync(
                updated!,
                _sri,
                _itemTypeRepo,
                _currentTenant.TenantId,
                cancellationToken
            )
        );
    }
}
