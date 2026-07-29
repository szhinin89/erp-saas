using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Items.UseCases.AddItemVariant;

public sealed record AddItemVariantCommand(
    Guid ItemId,
    IReadOnlyList<VariantAttributeInput> Attributes,
    string? SkuOverride = null,
    int SortOrder = 0
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
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _user;

    public AddItemVariantCommandHandler(
        IItemRepository repository, ICurrentTenant tenant, ICurrentUser user)
    {
        _repository = repository;
        _currentTenant = tenant;
        _user = user;
    }

    public async Task<Result<ItemVariantDto>> Handle(AddItemVariantCommand cmd, CancellationToken cancellationToken)
    {
        var item = await _repository.GetByIdAsync(cmd.ItemId, _currentTenant.TenantId, cancellationToken);
        if (item is null)
            return Result<ItemVariantDto>.NotFound("Ítem no encontrado.");

        var axisAttributes = cmd.Attributes
            .Select(a => (a.AttributeDefinitionId, a.Value))
            .ToList()
            .AsReadOnly();

        try
        {
            var variant = item.AddVariant(axisAttributes, cmd.SkuOverride, cmd.SortOrder, _user.UserId);

            // SKU de variante único por tenant (Fase 6) — no solo dentro del propio ítem.
            // El SKU final (con override o autogenerado a partir de los atributos) recién
            // se conoce tras AddVariant; se verifica contra el catálogo completo antes de
            // persistir, mismo patrón ya aplicado a barcode/código de proveedor en Fase 2.
            if (await _repository.VariantSkuExistsAsync(variant.SKU, _currentTenant.TenantId, cancellationToken))
                return Result<ItemVariantDto>.Conflict($"Ya existe una variante con SKU '{variant.SKU}' en otro ítem.");

            // Explicitly register the new variant so EF Core's change tracker can persist it.
            // Required because Item._variants is a private List<T> (not an ObservableCollection).
            await _repository.TrackVariantAsync(variant, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);

            return Result<ItemVariantDto>.Success(new ItemVariantDto(
                variant.Id,
                variant.SKU,
                variant.Name,
                variant.IsDefault,
                variant.SortOrder,
                variant.IsActive,
                variant.Attributes.Select(a => new VariantAttributeDto(a.AttributeDefinitionId, a.Value)).ToList(),
                variant.Barcodes.Select(b => new VariantBarcodeDto(b.Id, b.Code, b.BarcodeType, b.IsPrimary)).ToList()));
        }
        catch (InvalidOperationException ex)
        {
            return Result<ItemVariantDto>.Conflict(ex.Message);
        }
    }
}
