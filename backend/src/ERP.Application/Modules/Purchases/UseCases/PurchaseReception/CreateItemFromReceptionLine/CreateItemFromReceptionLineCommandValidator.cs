using FluentValidation;

namespace ERP.Application.Modules.Purchases.UseCases.PurchaseReception.CreateItemFromReceptionLine;

public sealed class CreateItemFromReceptionLineCommandValidator : AbstractValidator<CreateItemFromReceptionLineCommand>
{
    public CreateItemFromReceptionLineCommandValidator()
    {
        RuleFor(x => x.PurchaseReceptionLineId)
            .NotEmpty().WithMessage("La línea de recepción es obligatoria.");

        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("El SKU es obligatorio.")
            .MaximumLength(50).WithMessage("El SKU no puede exceder 50 caracteres.")
            .Matches(@"^[A-Za-z0-9\-_\.]+$").WithMessage("El SKU solo puede contener letras, números, guiones, puntos y guiones bajos.");

        RuleFor(x => x.ShortName)
            .NotEmpty().WithMessage("El nombre corto es obligatorio.")
            .MaximumLength(50).WithMessage("El nombre corto no puede exceder 50 caracteres.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("La descripción es obligatoria.")
            .MaximumLength(254).WithMessage("La descripción no puede exceder 254 caracteres.");

        RuleFor(x => x.ItemTypeId)
            .NotEmpty().WithMessage("El tipo de ítem es obligatorio.");

        RuleFor(x => x.CategoryNodeId)
            .NotEmpty().WithMessage("La categoría es obligatoria.");

        RuleFor(x => x.BrandId)
            .NotEmpty().WithMessage("La marca es obligatoria.");

        RuleFor(x => x.DefaultUomCode)
            .NotEmpty().WithMessage("La unidad de medida base es obligatoria.")
            .MaximumLength(10).WithMessage("El código UOM no puede exceder 10 caracteres.");

        RuleFor(x => x.BarcodeType)
            .NotEmpty().WithMessage("El tipo de código de barras es obligatorio.");
    }
}
