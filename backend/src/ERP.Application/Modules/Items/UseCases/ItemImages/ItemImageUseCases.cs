using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Items.UseCases.ItemImages;

// ── Input record ──────────────────────────────────────────────────────────
public record ImageInput(
    Guid StorageObjectId,
    string? AltText = null,
    bool IsMain = false,
    bool IsEcommerce = false,
    int SortOrder = 0,
    Guid? VariantId = null
);

// ── Commands ──────────────────────────────────────────────────────────────

/// <summary>Replaces all item images. Pass empty list to clear.</summary>
public sealed record ReplaceItemImagesCommand(Guid Id, IReadOnlyList<ImageInput> Images)
    : IRequest<Result<ItemDetailDto>>,
        ICompanyScopedRequest;

public sealed record DisableItemImageCommand(Guid ItemId, Guid ImageId)
    : IRequest<Result<bool>>,
        ICompanyScopedRequest;

// ── Validators ────────────────────────────────────────────────────────────

public sealed class ReplaceItemImagesCommandValidator : AbstractValidator<ReplaceItemImagesCommand>
{
    public ReplaceItemImagesCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Images)
            .Must(imgs => imgs.Count(i => i.IsMain) <= 1)
            .WithMessage("Solo una imagen puede ser principal.");
    }
}

// ── Handlers ──────────────────────────────────────────────────────────────

public sealed class ReplaceItemImagesCommandHandler
    : IRequestHandler<ReplaceItemImagesCommand, Result<ItemDetailDto>>
{
    private readonly IItemRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _user;
    private readonly ISriCatalogResolver _sri;
    private readonly IItemTypeRepository _itemTypeRepo;
    private readonly IBusinessPartnerRepository _businessPartnerRepo;

    public ReplaceItemImagesCommandHandler(
        IItemRepository repository,
        ICurrentTenant tenant,
        ICurrentUser user,
        ISriCatalogResolver sri,
        IItemTypeRepository itemTypeRepo,
        IBusinessPartnerRepository businessPartnerRepo
    )
    {
        _repository = repository;
        _currentTenant = tenant;
        _user = user;
        _sri = sri;
        _itemTypeRepo = itemTypeRepo;
        _businessPartnerRepo = businessPartnerRepo;
    }

    public async Task<Result<ItemDetailDto>> Handle(
        ReplaceItemImagesCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var item = await _repository.GetByIdLightAsync(
            cmd.Id,
            _currentTenant.TenantId,
            cancellationToken
        );
        if (item is null)
            return Result<ItemDetailDto>.NotFound("Ítem no encontrado.");

        var newImages = cmd
            .Images.OrderBy(i => i.SortOrder)
            .Select(i =>
                ItemImage.Create(
                    cmd.Id,
                    item.TenantId,
                    i.StorageObjectId,
                    i.AltText,
                    i.IsMain,
                    i.IsEcommerce,
                    i.SortOrder,
                    _user.UserId,
                    i.VariantId
                )
            )
            .ToList();

        await _repository.ReplaceImagesAsync(cmd.Id, newImages, cancellationToken);

        item.UpdateClassification(item.CategoryNodeId, item.BrandId, _user.UserId);
        await _repository.SaveChangesAsync(cancellationToken);

        var updated = await _repository.GetByIdAsync(cmd.Id, item.TenantId, cancellationToken);
        return Result<ItemDetailDto>.Success(
            await ItemMappingService.ToDetailDtoAsync(
                updated!,
                _sri,
                _itemTypeRepo,
                _currentTenant.TenantId,
                cancellationToken,
                _businessPartnerRepo
            )
        );
    }
}

public sealed class DisableItemImageCommandHandler
    : IRequestHandler<DisableItemImageCommand, Result<bool>>
{
    private readonly IItemRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _user;

    public DisableItemImageCommandHandler(
        IItemRepository repository,
        ICurrentTenant tenant,
        ICurrentUser user
    )
    {
        _repository = repository;
        _currentTenant = tenant;
        _user = user;
    }

    public async Task<Result<bool>> Handle(
        DisableItemImageCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var item = await _repository.GetByIdAsync(
            cmd.ItemId,
            _currentTenant.TenantId,
            cancellationToken
        );
        if (item is null)
            return Result<bool>.NotFound("Ítem no encontrado.");

        var image = item.Images.FirstOrDefault(i => i.Id == cmd.ImageId);
        if (image is null)
            return Result<bool>.NotFound("Imagen no encontrada.");

        image.Disable(_user.UserId);
        // Mark item as updated
        item.UpdateSaleConfig(item.SaleConfig, _user.UserId);
        await _repository.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
