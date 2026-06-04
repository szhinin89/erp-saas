using FluentValidation;
using MediatR;
using ERP.Application.Common;
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
    private readonly ICurrentSubscriber _subscriber;
    private readonly ICurrentUser _user;

    public ReplaceItemSubstitutesCommandHandler(
        IItemRepository repository, ICurrentSubscriber subscriber, ICurrentUser user)
    { _repository = repository; _subscriber = subscriber; _user = user; }

    public async Task<Result<ItemDetailDto>> Handle(ReplaceItemSubstitutesCommand cmd, CancellationToken ct)
    {
        var item = await _repository.GetByIdLightAsync(cmd.Id, _subscriber.SubscriberId, ct);
        if (item is null) return Result<ItemDetailDto>.NotFound("Ítem no encontrado.");

        if (cmd.Substitutes.Any(s => s.SubstituteItemId == cmd.Id))
            return Result<ItemDetailDto>.ValidationFailure("Un ítem no puede ser sustituto de sí mismo.");

        var newSubstitutes = cmd.Substitutes
            .Select(s => ItemSubstitute.Create(cmd.Id, item.SubscriberId, s.SubstituteItemId, s.Priority, s.Note))
            .ToList();

        await _repository.ReplaceSubstitutesAsync(cmd.Id, newSubstitutes, ct);
        item.UpdateClassification(item.CategoryNodeId, item.BrandId, _user.UserId);
        await _repository.SaveChangesAsync(ct);

        var updated = await _repository.GetByIdAsync(cmd.Id, item.SubscriberId, ct);
        return Result<ItemDetailDto>.Success(ItemMappingService.ToDetailDto(updated!));
    }
}
