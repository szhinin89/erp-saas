using MediatR;
using ERP.Application.Common;
using ERP.Application.Kits.DTOs;
using ERP.Domain.Modules.Items.Enums;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Kits.Entities;
using ERP.Domain.Modules.Kits.Enums;
using ERP.Domain.Modules.Kits.Interfaces;
using FluentValidation;

namespace ERP.Application.Kits.UseCases.CreateKit;

public sealed record CreateKitCommand(
    Guid    ItemId,
    KitType KitType
) : IRequest<Result<KitDto>>, ICompanyScopedRequest;

public sealed class CreateKitCommandValidator : AbstractValidator<CreateKitCommand>
{
    public CreateKitCommandValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.KitType).IsInEnum();
    }
}

public sealed class CreateKitCommandHandler
    : IRequestHandler<CreateKitCommand, Result<KitDto>>
{
    private readonly IKitRepository _kits;
    private readonly IItemRepository _items;
    private readonly ICurrentSubscriber _subscriber;
    private readonly ICurrentUser _user;

    public CreateKitCommandHandler(
        IKitRepository kits, IItemRepository items,
        ICurrentSubscriber subscriber, ICurrentUser user)
    {
        _kits       = kits;
        _items      = items;
        _subscriber = subscriber;
        _user       = user;
    }

    public async Task<Result<KitDto>> Handle(CreateKitCommand cmd, CancellationToken ct)
    {
        var item = await _items.GetByIdLightAsync(cmd.ItemId, _subscriber.SubscriberId, ct);
        if (item is null)
            return Result<KitDto>.NotFound("Ítem no encontrado.");

        if (item.ItemType is not (ItemType.Kit or ItemType.Bundle))
            return Result<KitDto>.ValidationFailure("El ítem debe ser de tipo Kit o Bundle.");

        if (await _kits.ExistsByItemIdAsync(cmd.ItemId, ct))
            return Result<KitDto>.Conflict("Ya existe una receta para este ítem.");

        var kit = Kit.Create(_subscriber.SubscriberId, cmd.ItemId, cmd.KitType, _user.UserId);
        await _kits.AddAsync(kit, ct);
        await _kits.SaveChangesAsync(ct);

        return Result<KitDto>.Success(new KitDto(kit.Id, kit.ItemId, kit.KitType.ToString(), kit.IsActive, []));
    }
}
