using FluentValidation;
using MediatR;
using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Interfaces;

namespace ERP.Application.Items.UseCases.UpdateItemVariant;

public sealed record UpdateItemVariantCommand(
    Guid ItemId,
    Guid VariantId,
    int  SortOrder,
    bool IsDefault = false)
    : IRequest<Result<ItemVariantDto>>, ICompanyScopedRequest;

public sealed class UpdateItemVariantCommandValidator : AbstractValidator<UpdateItemVariantCommand>
{
    public UpdateItemVariantCommandValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.VariantId).NotEmpty();
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateItemVariantCommandHandler
    : IRequestHandler<UpdateItemVariantCommand, Result<ItemVariantDto>>
{
    private readonly IItemRepository _repository;
    private readonly ICurrentSubscriber _subscriber;
    private readonly ICurrentUser _user;

    public UpdateItemVariantCommandHandler(
        IItemRepository repository, ICurrentSubscriber subscriber, ICurrentUser user)
    { _repository = repository; _subscriber = subscriber; _user = user; }

    public async Task<Result<ItemVariantDto>> Handle(UpdateItemVariantCommand cmd, CancellationToken ct)
    {
        var item = await _repository.GetByIdAsync(cmd.ItemId, _subscriber.SubscriberId, ct);
        if (item is null) return Result<ItemVariantDto>.NotFound("Ítem no encontrado.");

        var variant = item.Variants.FirstOrDefault(v => v.Id == cmd.VariantId);
        if (variant is null) return Result<ItemVariantDto>.NotFound("Variante no encontrada.");

        variant.UpdateSortOrder(cmd.SortOrder);
        if (cmd.IsDefault) variant.SetAsDefault();

        // SetUpdated on the aggregate root
        item.UpdateClassification(item.CategoryNodeId, item.BrandId, _user.UserId);
        await _repository.SaveChangesAsync(ct);

        return Result<ItemVariantDto>.Success(new ItemVariantDto(
            variant.Id, variant.SKU, variant.Name, variant.IsDefault, variant.SortOrder, variant.IsActive,
            variant.Attributes.Select(a => new VariantAttributeDto(a.AttributeDefinitionId, a.Value)).ToList(),
            variant.Barcodes.Where(b => b.IsActive)
                .Select(b => new VariantBarcodeDto(b.Id, b.Code, b.BarcodeType.ToString())).ToList()));
    }
}
