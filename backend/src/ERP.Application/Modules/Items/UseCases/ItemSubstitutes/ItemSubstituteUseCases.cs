using FluentValidation;
using MediatR;
using ERP.Application.Common;
using ERP.Application.Items;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;

namespace ERP.Application.Items.UseCases.ItemSubstitutes;

public record SubstituteInput(Guid SubstituteItemId, int Priority = 1, string? Note = null);

public sealed record ReplaceItemSubstitutesCommand(
    Guid Id,
    IReadOnlyList<SubstituteInput> Substitutes)
    : IRequest<Result<ItemDetailDto>>, ICompanyScopedRequest;

public sealed class ReplaceItemSubstitutesCommandValidator
    : AbstractValidator<ReplaceItemSubstitutesCommand>
{
    public ReplaceItemSubstitutesCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleForEach(x => x.Substitutes).ChildRules(s =>
        {
            s.RuleFor(x => x.SubstituteItemId).NotEmpty()
                .WithMessage("El ítem sustituto es obligatorio.");
            s.RuleFor(x => x.Priority).GreaterThan(0);
        });
    }
}

public sealed class ReplaceItemSubstitutesCommandHandler
    : IRequestHandler<ReplaceItemSubstitutesCommand, Result<ItemDetailDto>>
{
    private readonly IItemRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _user;
    private readonly ISriCatalogResolver _sri;
    private readonly IItemTypeRepository _itemTypeRepo;

    public ReplaceItemSubstitutesCommandHandler(
        IItemRepository repository, ICurrentTenant tenant, ICurrentUser user, ISriCatalogResolver sri,
        IItemTypeRepository itemTypeRepo)
    { _repository = repository; _currentTenant = tenant; _user = user; _sri = sri; _itemTypeRepo = itemTypeRepo; }

    public async Task<Result<ItemDetailDto>> Handle(ReplaceItemSubstitutesCommand cmd, CancellationToken cancellationToken)
    {
        var item = await _repository.GetByIdLightAsync(cmd.Id, _currentTenant.TenantId, cancellationToken);
        if (item is null) return Result<ItemDetailDto>.NotFound("Ítem no encontrado.");

        if (cmd.Substitutes.Any(s => s.SubstituteItemId == cmd.Id))
            return Result<ItemDetailDto>.ValidationFailure("Un ítem no puede ser sustituto de sí mismo.");

        var newSubstitutes = cmd.Substitutes
            .Select(s => ItemSubstitute.Create(cmd.Id, item.TenantId, s.SubstituteItemId, s.Priority, s.Note, _user.UserId))
            .ToList();

        await _repository.ReplaceSubstitutesAsync(cmd.Id, newSubstitutes, cancellationToken);
        item.UpdateClassification(item.CategoryNodeId, item.BrandId, _user.UserId);
        await _repository.SaveChangesAsync(cancellationToken);

        var updated = await _repository.GetByIdAsync(cmd.Id, item.TenantId, cancellationToken);
        return Result<ItemDetailDto>.Success(
            await ItemMappingService.ToDetailDtoAsync(updated!, _sri, _itemTypeRepo, _currentTenant.TenantId, cancellationToken));
    }
}
