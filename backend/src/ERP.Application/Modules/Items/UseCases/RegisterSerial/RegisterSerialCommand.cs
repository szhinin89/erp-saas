using MediatR;
using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using FluentValidation;

namespace ERP.Application.Items.UseCases.RegisterSerial;

public sealed record RegisterSerialCommand(
    Guid      ItemId,
    Guid      WarehouseId,
    IReadOnlyList<string> Serials,
    Guid?     VariantId = null,
    Guid?     LotId     = null,
    string?   Notes     = null
) : IRequest<Result<IReadOnlyList<SerialDto>>>, ICompanyScopedRequest;

public sealed class RegisterSerialCommandValidator : AbstractValidator<RegisterSerialCommand>
{
    public RegisterSerialCommandValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Serials)
            .NotEmpty().WithMessage("Debe especificar al menos un número de serie.")
            .Must(s => s.All(x => !string.IsNullOrWhiteSpace(x)))
            .WithMessage("Todos los números de serie deben tener valor.")
            .Must(s => s.Count == s.Distinct().Count())
            .WithMessage("Los números de serie no pueden repetirse.");
    }
}

public sealed class RegisterSerialCommandHandler
    : IRequestHandler<RegisterSerialCommand, Result<IReadOnlyList<SerialDto>>>
{
    private readonly ISerialRepository _serials;
    private readonly IItemRepository _items;
    private readonly ICurrentSubscriber _subscriber;
    private readonly ICurrentCompany _company;
    private readonly ICurrentUser _user;

    public RegisterSerialCommandHandler(
        ISerialRepository serials, IItemRepository items,
        ICurrentSubscriber subscriber, ICurrentCompany company, ICurrentUser user)
    {
        _serials    = serials;
        _items      = items;
        _subscriber = subscriber;
        _company    = company;
        _user       = user;
    }

    public async Task<Result<IReadOnlyList<SerialDto>>> Handle(
        RegisterSerialCommand cmd, CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var companyId    = _company.CompanyId;

        var item = await _items.GetByIdLightAsync(cmd.ItemId, subscriberId, ct);
        if (item is null)
            return Result<IReadOnlyList<SerialDto>>.NotFound("Ítem no encontrado.");

        if (!item.StockConfig.TracksSeries)
            return Result<IReadOnlyList<SerialDto>>.ValidationFailure("El ítem no está configurado para rastreo por serie.");

        // Check duplicates against existing serials
        foreach (var serial in cmd.Serials)
        {
            if (await _serials.ExistsAsync(cmd.ItemId, serial, ct))
                return Result<IReadOnlyList<SerialDto>>.Conflict(
                    $"El número de serie '{serial}' ya existe para este ítem.");
        }

        var entities = cmd.Serials.Select(s => SerialNumber.Create(
            subscriberId, companyId, s,
            cmd.ItemId, cmd.WarehouseId, _user.UserId,
            cmd.VariantId, cmd.LotId, cmd.Notes)).ToList();

        await _serials.AddRangeAsync(entities, ct);
        await _serials.SaveChangesAsync(ct);

        var dtos = entities.Select(sn => new SerialDto(
            sn.Id, sn.Serial, sn.ItemId, sn.VariantId, sn.WarehouseId,
            sn.LotId, sn.Status.ToString(), sn.AcquiredAt, sn.SoldAt, sn.DocumentRef)).ToList();

        return Result<IReadOnlyList<SerialDto>>.Success(dtos);
    }
}
