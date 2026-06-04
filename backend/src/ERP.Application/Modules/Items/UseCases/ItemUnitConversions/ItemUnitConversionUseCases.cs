using FluentValidation;
using MediatR;
using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;

namespace ERP.Application.Items.UseCases.ItemUnitConversions;

public record UnitConversionInput(string FromUomCode, string ToUomCode, decimal Factor);

public sealed record ReplaceItemUnitConversionsCommand(
    Guid Id,
    IReadOnlyList<UnitConversionInput> Conversions)
    : IRequest<Result<ItemDetailDto>>, ICompanyScopedRequest;

public sealed class ReplaceItemUnitConversionsCommandValidator
    : AbstractValidator<ReplaceItemUnitConversionsCommand>
{
    public ReplaceItemUnitConversionsCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleForEach(x => x.Conversions).ChildRules(c =>
        {
            c.RuleFor(x => x.FromUomCode).NotEmpty().MaximumLength(10);
            c.RuleFor(x => x.ToUomCode).NotEmpty().MaximumLength(10);
            c.RuleFor(x => x.Factor).GreaterThan(0).WithMessage("El factor debe ser mayor que cero.");
        });
    }
}

public sealed class ReplaceItemUnitConversionsCommandHandler
    : IRequestHandler<ReplaceItemUnitConversionsCommand, Result<ItemDetailDto>>
{
    private readonly IItemRepository _repository;
    private readonly ICurrentSubscriber _subscriber;
    private readonly ICurrentUser _user;

    public ReplaceItemUnitConversionsCommandHandler(
        IItemRepository repository, ICurrentSubscriber subscriber, ICurrentUser user)
    { _repository = repository; _subscriber = subscriber; _user = user; }

    public async Task<Result<ItemDetailDto>> Handle(ReplaceItemUnitConversionsCommand cmd, CancellationToken ct)
    {
        var item = await _repository.GetByIdLightAsync(cmd.Id, _subscriber.SubscriberId, ct);
        if (item is null) return Result<ItemDetailDto>.NotFound("Ítem no encontrado.");

        var newConversions = cmd.Conversions
            .Select(c => ItemUnitConversion.Create(
                cmd.Id, item.SubscriberId,
                c.FromUomCode, c.ToUomCode, c.Factor))
            .ToList();

        try { await _repository.ReplaceUnitConversionsAsync(cmd.Id, newConversions, ct); }
        catch (ArgumentException ex) { return Result<ItemDetailDto>.ValidationFailure(ex.Message); }

        item.UpdateClassification(item.CategoryNodeId, item.BrandId, _user.UserId);
        await _repository.SaveChangesAsync(ct);

        var updated = await _repository.GetByIdAsync(cmd.Id, item.SubscriberId, ct);
        return Result<ItemDetailDto>.Success(ItemMappingService.ToDetailDto(updated!));
    }
}
