using FluentValidation;

namespace ERP.Application.Modules.Settings.Operations.UseCases.UpdateOperationalPreferences;

/// <summary>
/// Defensa en profundidad frente al Validator de cada ConfigurationDefinition (que
/// OrgSettingsRepository.UpsertAsync re-valida de todas formas) — mensajes más específicos en el
/// borde de la API, mismo rol que UpdateCompanyEmailSettingsCommandValidator.
/// </summary>
public sealed class UpdateOperationalPreferencesCommandValidator
    : AbstractValidator<UpdateOperationalPreferencesCommand>
{
    private static readonly string[] ReceiptModes = ["AskBeforePrint", "AlwaysPrint", "NeverAutoPrint"];
    private static readonly string[] PaperWidths = ["80mm", "58mm"];
    private static readonly string[] Languages = ["es", "en"];

    public UpdateOperationalPreferencesCommandValidator()
    {
        When(
            c => c.SalesPos is not null,
            () =>
            {
                RuleFor(c => c.SalesPos!.MaxDiscountPercent)
                    .InclusiveBetween(0m, 100m)
                    .WithMessage("El descuento máximo debe estar entre 0 y 100.");
                RuleFor(c => c.SalesPos!.RequireCustomerAboveAmount)
                    .GreaterThanOrEqualTo(0m)
                    .When(c => c.SalesPos!.RequireCustomerAboveAmount.HasValue)
                    .WithMessage("El monto para exigir cliente debe ser mayor o igual a 0.");
            }
        );

        When(
            c => c.Cash is not null,
            () =>
                RuleFor(c => c.Cash!.MaxAllowedDifference)
                    .GreaterThanOrEqualTo(0m)
                    .WithMessage("La diferencia máxima permitida debe ser mayor o igual a 0.")
        );

        When(
            c => c.Inventory is not null,
            () =>
                RuleFor(c => c.Inventory!.LargeAdjustmentThresholdAmount)
                    .GreaterThanOrEqualTo(0m)
                    .WithMessage("El umbral de ajuste grande debe ser mayor o igual a 0.")
        );

        When(
            c => c.Printing is not null,
            () =>
            {
                RuleFor(c => c.Printing!.SalesReceiptMode)
                    .Must(v => ReceiptModes.Contains(v))
                    .WithMessage(
                        "El modo de impresión debe ser AskBeforePrint, AlwaysPrint o NeverAutoPrint."
                    );
                RuleFor(c => c.Printing!.SalesReceiptCopies)
                    .InclusiveBetween(1, 3)
                    .WithMessage("El número de copias debe estar entre 1 y 3.");
                RuleFor(c => c.Printing!.SalesReceiptPaperWidth)
                    .Must(v => PaperWidths.Contains(v))
                    .WithMessage("El ancho de papel debe ser 80mm o 58mm.");
            }
        );

        When(
            c => c.ElectronicDocuments is not null,
            () =>
                RuleFor(c => c.ElectronicDocuments!.MaxRetryAttempts)
                    .InclusiveBetween(1, 10)
                    .WithMessage("Los reintentos máximos deben estar entre 1 y 10.")
        );

        When(
            c => c.Notifications is not null,
            () =>
                RuleFor(c => c.Notifications!.DefaultLanguage)
                    .Must(v => Languages.Contains(v))
                    .WithMessage("El idioma por defecto debe ser 'es' o 'en'.")
        );
    }
}
