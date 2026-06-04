using MediatR;
using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using FluentValidation;

namespace ERP.Application.Items.UseCases.RegisterLot;

public sealed record RegisterLotCommand(
    Guid      ItemId,
    Guid      WarehouseId,
    string    LotNumber,
    decimal   InitialQty,
    Guid?     VariantId       = null,
    DateOnly? ManufactureDate = null,
    DateOnly? ExpirationDate  = null,
    string?   Notes           = null
) : IRequest<Result<LotDto>>, ICompanyScopedRequest;

public sealed class RegisterLotCommandValidator : AbstractValidator<RegisterLotCommand>
{
    public RegisterLotCommandValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.LotNumber).NotEmpty().MaximumLength(100);
        RuleFor(x => x.InitialQty).GreaterThan(0).WithMessage("La cantidad inicial debe ser mayor que cero.");
        RuleFor(x => x)
            .Must(x => !x.ExpirationDate.HasValue || !x.ManufactureDate.HasValue || x.ExpirationDate >= x.ManufactureDate)
            .WithMessage("La fecha de vencimiento no puede ser anterior a la de fabricación.")
            .WithName("Dates");
    }
}

public sealed class RegisterLotCommandHandler
    : IRequestHandler<RegisterLotCommand, Result<LotDto>>
{
    private readonly ILotRepository _lots;
    private readonly IItemRepository _items;
    private readonly ICurrentSubscriber _subscriber;
    private readonly ICurrentCompany _company;
    private readonly ICurrentUser _user;

    public RegisterLotCommandHandler(
        ILotRepository lots, IItemRepository items,
        ICurrentSubscriber subscriber, ICurrentCompany company, ICurrentUser user)
    {
        _lots       = lots;
        _items      = items;
        _subscriber = subscriber;
        _company    = company;
        _user       = user;
    }

    public async Task<Result<LotDto>> Handle(RegisterLotCommand cmd, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;

        var item = await _items.GetByIdLightAsync(cmd.ItemId, subscriberId, ct);
        if (item is null)
            return Result<LotDto>.NotFound("Ítem no encontrado.");

        if (!item.StockConfig.TracksLot)
            return Result<LotDto>.ValidationFailure("El ítem no está configurado para rastreo por lote.");

        if (await _lots.ExistsAsync(cmd.ItemId, cmd.LotNumber, ct))
            return Result<LotDto>.Conflict($"Ya existe un lote '{cmd.LotNumber}' para este ítem.");

        Lot lot;
        try
        {
            lot = Lot.Create(
                subscriberId, _company.CompanyId,
                cmd.LotNumber, cmd.ItemId, cmd.WarehouseId, cmd.InitialQty,
                _user.UserId, cmd.VariantId, cmd.ManufactureDate, cmd.ExpirationDate, cmd.Notes);
        }
        catch (ArgumentException ex)
        {
            return Result<LotDto>.ValidationFailure(ex.Message);
        }

        await _lots.AddAsync(lot, ct);
        await _lots.SaveChangesAsync(ct);

        return Result<LotDto>.Success(new LotDto(
            lot.Id, lot.LotNumber, lot.ItemId, lot.VariantId, lot.WarehouseId,
            lot.ManufactureDate, lot.ExpirationDate,
            lot.InitialQty, lot.CurrentQty, lot.Status.ToString(),
            lot.Notes, lot.CreatedAt));
    }
}
