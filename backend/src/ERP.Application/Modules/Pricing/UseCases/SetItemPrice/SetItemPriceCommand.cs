using MediatR;
using ERP.Application.Common;
using ERP.Application.Pricing.DTOs;
using ERP.Domain.Modules.Pricing.Interfaces;
using FluentValidation;

namespace ERP.Application.Pricing.UseCases.SetItemPrice;

public sealed record SetItemPriceCommand(
    Guid     PriceListId,
    Guid     ItemId,
    string   UomCode,
    decimal  Price,
    decimal  MinQty    = 1,
    Guid?    VariantId = null
) : IRequest<Result<PriceListEntryDto>>, ICompanyScopedRequest;

public sealed class SetItemPriceCommandValidator : AbstractValidator<SetItemPriceCommand>
{
    public SetItemPriceCommandValidator()
    {
        RuleFor(x => x.PriceListId).NotEmpty();
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.UomCode).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).WithMessage("El precio no puede ser negativo.");
        RuleFor(x => x.MinQty).GreaterThan(0).WithMessage("La cantidad mínima debe ser mayor que cero.");
    }
}

public sealed class SetItemPriceCommandHandler
    : IRequestHandler<SetItemPriceCommand, Result<PriceListEntryDto>>
{
    private readonly IPriceListRepository _repository;

    public SetItemPriceCommandHandler(IPriceListRepository repository) => _repository = repository;

    public async Task<Result<PriceListEntryDto>> Handle(SetItemPriceCommand cmd, CancellationToken ct)
    {
        var priceList = await _repository.GetByIdAsync(cmd.PriceListId, ct);
        if (priceList is null)
            return Result<PriceListEntryDto>.NotFound("Lista de precios no encontrada.");

        try
        {
            var (entry, isNew) = priceList.SetItemPrice(
                cmd.ItemId, cmd.VariantId, cmd.UomCode, cmd.Price, cmd.MinQty);

            if (isNew)
                await _repository.TrackEntryAsync(entry, ct);
            await _repository.SaveChangesAsync(ct);

            return Result<PriceListEntryDto>.Success(new PriceListEntryDto(
                entry.Id, entry.ItemId, entry.VariantId,
                entry.UomCode, entry.Price, entry.MinQty, entry.IsActive));
        }
        catch (ArgumentException ex)
        {
            return Result<PriceListEntryDto>.ValidationFailure(ex.Message);
        }
    }
}
