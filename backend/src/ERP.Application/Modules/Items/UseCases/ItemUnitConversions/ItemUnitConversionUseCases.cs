using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Items.UseCases.ItemUnitConversions;

public record UnitConversionInput(string FromUomCode, string ToUomCode, decimal Factor);

public sealed record ReplaceItemUnitConversionsCommand(
    Guid Id,
    IReadOnlyList<UnitConversionInput> Conversions
) : IRequest<Result<ItemDetailDto>>, ICompanyScopedRequest;

public sealed class ReplaceItemUnitConversionsCommandValidator
    : AbstractValidator<ReplaceItemUnitConversionsCommand>
{
    public ReplaceItemUnitConversionsCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleForEach(x => x.Conversions)
            .ChildRules(c =>
            {
                c.RuleFor(x => x.FromUomCode).NotEmpty().MaximumLength(10);
                c.RuleFor(x => x.ToUomCode).NotEmpty().MaximumLength(10);
                c.RuleFor(x => x.Factor)
                    .GreaterThan(0)
                    .WithMessage("El factor debe ser mayor que cero.");
            });
    }
}

public sealed class ReplaceItemUnitConversionsCommandHandler
    : IRequestHandler<ReplaceItemUnitConversionsCommand, Result<ItemDetailDto>>
{
    private readonly IItemRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _user;
    private readonly ISriCatalogResolver _sri;
    private readonly IItemTypeRepository _itemTypeRepo;

    public ReplaceItemUnitConversionsCommandHandler(
        IItemRepository repository,
        ICurrentTenant tenant,
        ICurrentUser user,
        ISriCatalogResolver sri,
        IItemTypeRepository itemTypeRepo
    )
    {
        _repository = repository;
        _currentTenant = tenant;
        _user = user;
        _sri = sri;
        _itemTypeRepo = itemTypeRepo;
    }

    public async Task<Result<ItemDetailDto>> Handle(
        ReplaceItemUnitConversionsCommand cmd,
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

        var newConversions = cmd
            .Conversions.Select(c =>
                ItemUnitConversion.Create(
                    cmd.Id,
                    item.TenantId,
                    c.FromUomCode,
                    c.ToUomCode,
                    c.Factor,
                    _user.UserId
                )
            )
            .ToList();

        try
        {
            await _repository.ReplaceUnitConversionsAsync(
                cmd.Id,
                newConversions,
                cancellationToken
            );
        }
        catch (ArgumentException ex)
        {
            return Result<ItemDetailDto>.ValidationFailure(ex.Message);
        }

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
