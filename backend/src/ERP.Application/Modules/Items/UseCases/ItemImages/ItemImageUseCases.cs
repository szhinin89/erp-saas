using FluentValidation;
using MediatR;
using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;

namespace ERP.Application.Items.UseCases.ItemImages;

// ── Input record ──────────────────────────────────────────────────────────
public record ImageInput(
    Guid    StorageObjectId,
    string? AltText     = null,
    bool    IsMain      = false,
    bool    IsEcommerce = false,
    int     SortOrder   = 0,
    Guid?   VariantId   = null
);

// ── Commands ──────────────────────────────────────────────────────────────

/// <summary>Replaces all item images. Pass empty list to clear.</summary>
public sealed record ReplaceItemImagesCommand(
    Guid Id,
    IReadOnlyList<ImageInput> Images)
    : IRequest<Result<ItemDetailDto>>, ICompanyScopedRequest;

public sealed record DisableItemImageCommand(Guid ItemId, Guid ImageId)
    : IRequest<Result<bool>>, ICompanyScopedRequest;

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
    private readonly ICurrentSubscriber _subscriber;
    private readonly ICurrentUser _user;

    public ReplaceItemImagesCommandHandler(
        IItemRepository repository, ICurrentSubscriber subscriber, ICurrentUser user)
    { _repository = repository; _subscriber = subscriber; _user = user; }

    public async Task<Result<ItemDetailDto>> Handle(ReplaceItemImagesCommand cmd, CancellationToken ct)
    {
        var item = await _repository.GetByIdLightAsync(cmd.Id, _subscriber.SubscriberId, ct);
        if (item is null) return Result<ItemDetailDto>.NotFound("Ítem no encontrado.");

        // Build new image entities from the command inputs
        var newImages = cmd.Images
            .OrderBy(i => i.SortOrder)
            .Select(i => ItemImage.Create(
                cmd.Id, item.SubscriberId,
                i.StorageObjectId, i.AltText,
                i.IsMain, i.IsEcommerce, i.SortOrder,
                i.VariantId))
            .ToList();

        // Replace at DB level (bypass backing field limitation)
        await _repository.ReplaceImagesAsync(cmd.Id, newImages, ct);

        // Touch the aggregate root so UpdatedAt is refreshed
        item.UpdateClassification(item.CategoryNodeId, item.BrandId, _user.UserId);
        await _repository.SaveChangesAsync(ct);

        // Re-load with full detail for response
        var updated = await _repository.GetByIdAsync(cmd.Id, item.SubscriberId, ct);
        return Result<ItemDetailDto>.Success(ItemMappingService.ToDetailDto(updated!));
    }
}

public sealed class DisableItemImageCommandHandler
    : IRequestHandler<DisableItemImageCommand, Result<bool>>
{
    private readonly IItemRepository _repository;
    private readonly ICurrentSubscriber _subscriber;
    private readonly ICurrentUser _user;

    public DisableItemImageCommandHandler(
        IItemRepository repository, ICurrentSubscriber subscriber, ICurrentUser user)
    { _repository = repository; _subscriber = subscriber; _user = user; }

    public async Task<Result<bool>> Handle(DisableItemImageCommand cmd, CancellationToken ct)
    {
        var item = await _repository.GetByIdAsync(cmd.ItemId, _subscriber.SubscriberId, ct);
        if (item is null) return Result<bool>.NotFound("Ítem no encontrado.");

        var image = item.Images.FirstOrDefault(i => i.Id == cmd.ImageId);
        if (image is null) return Result<bool>.NotFound("Imagen no encontrada.");

        image.Disable();
        // Mark item as updated
        item.UpdateSaleConfig(item.SaleConfig, _user.UserId);
        await _repository.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
