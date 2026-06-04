using MediatR;
using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Interfaces;
using FluentValidation;

namespace ERP.Application.Items.UseCases.AddItemVariant;

public sealed record AddItemVariantCommand(
    Guid                                   ItemId,
    IReadOnlyList<VariantAttributeInput>   Attributes,
    string?                                SkuOverride = null,
    int                                    SortOrder   = 0
) : IRequest<Result<ItemVariantDto>>, ICompanyScopedRequest;

public record VariantAttributeInput(Guid AttributeDefinitionId, string Value);

// ── Validator ─────────────────────────────────────────────────────────────

public sealed class AddItemVariantCommandValidator : AbstractValidator<AddItemVariantCommand>
{
    public AddItemVariantCommandValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty().WithMessage("El Id del ítem es obligatorio.");

        RuleFor(x => x.Attributes)
            .NotEmpty().WithMessage("Debe especificar al menos un atributo de variante.")
            .Must(attrs => attrs.All(a => a.AttributeDefinitionId != Guid.Empty))
            .WithMessage("Todos los atributos deben tener un AttributeDefinitionId válido.")
            .Must(attrs => attrs.All(a => !string.IsNullOrWhiteSpace(a.Value)))
            .WithMessage("Todos los valores de atributo son obligatorios.");

        RuleFor(x => x.SkuOverride)
            .MaximumLength(80).WithMessage("El SKU override no puede exceder 80 caracteres.")
            .When(x => x.SkuOverride is not null);
    }
}

// ── Handler ───────────────────────────────────────────────────────────────

public sealed class AddItemVariantCommandHandler
    : IRequestHandler<AddItemVariantCommand, Result<ItemVariantDto>>
{
    private readonly IItemRepository _repository;
    private readonly ICurrentSubscriber _subscriber;
    private readonly ICurrentUser _user;

    public AddItemVariantCommandHandler(
        IItemRepository repository, ICurrentSubscriber subscriber, ICurrentUser user)
    {
        _repository = repository;
        _subscriber = subscriber;
        _user       = user;
    }

    public async Task<Result<ItemVariantDto>> Handle(AddItemVariantCommand cmd, CancellationToken ct)
    {
        var item = await _repository.GetByIdAsync(cmd.ItemId, _subscriber.SubscriberId, ct);
        if (item is null)
            return Result<ItemVariantDto>.NotFound("Ítem no encontrado.");

        var axisAttributes = cmd.Attributes
            .Select(a => (a.AttributeDefinitionId, a.Value))
            .ToList()
            .AsReadOnly();

        try
        {
            var variant = item.AddVariant(axisAttributes, cmd.SkuOverride, cmd.SortOrder, _user.UserId);
            // Explicitly register the new variant so EF Core's change tracker can persist it.
            // Required because Item._variants is a private List<T> (not an ObservableCollection).
            await _repository.TrackVariantAsync(variant, ct);
            await _repository.SaveChangesAsync(ct);

            return Result<ItemVariantDto>.Success(new ItemVariantDto(
                variant.Id,
                variant.SKU,
                variant.Name,
                variant.IsDefault,
                variant.SortOrder,
                variant.IsActive,
                variant.Attributes.Select(a => new VariantAttributeDto(a.AttributeDefinitionId, a.Value)).ToList(),
                variant.Barcodes.Select(b => new VariantBarcodeDto(b.Id, b.Code, b.BarcodeType.ToString())).ToList()));
        }
        catch (InvalidOperationException ex)
        {
            return Result<ItemVariantDto>.Conflict(ex.Message);
        }
    }
}
