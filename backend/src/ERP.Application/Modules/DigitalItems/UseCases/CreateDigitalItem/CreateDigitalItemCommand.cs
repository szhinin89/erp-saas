using MediatR;
using ERP.Application.Common;
using ERP.Application.DigitalItems.DTOs;
using ERP.Domain.Modules.DigitalItems.Entities;
using ERP.Domain.Modules.DigitalItems.Enums;
using ERP.Domain.Modules.DigitalItems.Interfaces;
using ERP.Domain.Modules.Items.Enums;
using ERP.Domain.Modules.Items.Interfaces;
using FluentValidation;

namespace ERP.Application.DigitalItems.UseCases.CreateDigitalItem;

public sealed record CreateDigitalItemCommand(
    Guid           ItemId,
    LicenseType    LicenseType,
    DeliveryMethod DeliveryMethod,
    int?           MaxDownloads = null,
    int?           ValidityDays = null
) : IRequest<Result<DigitalItemDto>>, ICompanyScopedRequest;

public sealed class CreateDigitalItemCommandValidator : AbstractValidator<CreateDigitalItemCommand>
{
    public CreateDigitalItemCommandValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.LicenseType).IsInEnum();
        RuleFor(x => x.DeliveryMethod).IsInEnum();
        RuleFor(x => x.MaxDownloads).GreaterThan(0).When(x => x.MaxDownloads.HasValue);
        RuleFor(x => x.ValidityDays).GreaterThan(0).When(x => x.ValidityDays.HasValue);
    }
}

public sealed class CreateDigitalItemCommandHandler
    : IRequestHandler<CreateDigitalItemCommand, Result<DigitalItemDto>>
{
    private readonly IDigitalItemRepository _digitalItems;
    private readonly IItemRepository _items;
    private readonly ICurrentSubscriber _subscriber;
    private readonly ICurrentUser _user;

    public CreateDigitalItemCommandHandler(
        IDigitalItemRepository digitalItems, IItemRepository items,
        ICurrentSubscriber subscriber, ICurrentUser user)
    {
        _digitalItems = digitalItems;
        _items        = items;
        _subscriber   = subscriber;
        _user         = user;
    }

    public async Task<Result<DigitalItemDto>> Handle(CreateDigitalItemCommand cmd, CancellationToken ct)
    {
        var item = await _items.GetByIdLightAsync(cmd.ItemId, _subscriber.SubscriberId, ct);
        if (item is null)
            return Result<DigitalItemDto>.NotFound("Ítem no encontrado.");

        if (item.ItemType != ItemType.Digital)
            return Result<DigitalItemDto>.ValidationFailure("El ítem debe ser de tipo Digital.");

        if (await _digitalItems.ExistsByItemIdAsync(cmd.ItemId, ct))
            return Result<DigitalItemDto>.Conflict("Ya existe una configuración digital para este ítem.");

        var digitalItem = DigitalItem.Create(
            _subscriber.SubscriberId, cmd.ItemId,
            cmd.LicenseType, cmd.DeliveryMethod,
            _user.UserId, cmd.MaxDownloads, cmd.ValidityDays);

        await _digitalItems.AddAsync(digitalItem, ct);
        await _digitalItems.SaveChangesAsync(ct);

        return Result<DigitalItemDto>.Success(new DigitalItemDto(
            digitalItem.Id, digitalItem.ItemId,
            digitalItem.LicenseType.ToString(), digitalItem.DeliveryMethod.ToString(),
            digitalItem.MaxDownloads, digitalItem.ValidityDays, digitalItem.IsActive, []));
    }
}
