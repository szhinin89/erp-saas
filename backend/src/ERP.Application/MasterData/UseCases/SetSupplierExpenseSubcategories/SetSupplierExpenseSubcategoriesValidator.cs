using FluentValidation;

namespace ERP.Application.MasterData.UseCases.SetSupplierExpenseSubcategories;

public sealed class SetSupplierExpenseSubcategoriesValidator
    : AbstractValidator<SetSupplierExpenseSubcategoriesCommand>
{
    public const int MaxSubcategories = 500;

    public SetSupplierExpenseSubcategoriesValidator()
    {
        RuleFor(x => x.BusinessPartnerId).NotEmpty();
        RuleFor(x => x.ExpenseSubcategoryIds)
            .NotNull()
            .WithMessage("Debe indicar el conjunto de subcategorías (puede ser vacío).");
        RuleFor(x => x.ExpenseSubcategoryIds.Count)
            .LessThanOrEqualTo(MaxSubcategories)
            .When(x => x.ExpenseSubcategoryIds is not null)
            .WithMessage($"No se admiten más de {MaxSubcategories} subcategorías.");
        RuleForEach(x => x.ExpenseSubcategoryIds)
            .NotEmpty()
            .WithMessage("Los identificadores de subcategoría no pueden estar vacíos.");
        RuleFor(x => x.ExpenseSubcategoryIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .When(x => x.ExpenseSubcategoryIds is not null)
            .WithMessage("El conjunto de subcategorías no puede contener duplicados.");
    }
}
