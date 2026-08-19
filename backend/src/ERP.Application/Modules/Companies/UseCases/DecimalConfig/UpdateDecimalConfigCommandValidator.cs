using FluentValidation;

namespace ERP.Application.Modules.Companies.UseCases.DecimalConfig;

/// <summary>
/// CONFIG-FOUNDATION-P1-01: rango 0-6 decimales, igual al que ya aplicaba
/// <c>decimalSettingsSchema</c> en frontend (F-V1/F-V2) — ahora también reforzado en backend en
/// vez de solo clamparse en silencio al persistir. Un valor fuera de rango se rechaza aquí, no
/// se corrige a un valor "razonable" sin que el usuario lo sepa.
/// </summary>
public sealed class UpdateDecimalConfigCommandValidator : AbstractValidator<UpdateDecimalConfigCommand>
{
    private const int MinDecimals = 0;
    private const int MaxDecimals = 6;

    public UpdateDecimalConfigCommandValidator()
    {
        RuleFor(c => c.SalesUnitPrice).InclusiveBetween(MinDecimals, MaxDecimals);
        RuleFor(c => c.PurchaseUnitPrice).InclusiveBetween(MinDecimals, MaxDecimals);
        RuleFor(c => c.Quantity).InclusiveBetween(MinDecimals, MaxDecimals);
        RuleFor(c => c.Percentage).InclusiveBetween(MinDecimals, MaxDecimals);
        RuleFor(c => c.TotalAmount).InclusiveBetween(MinDecimals, MaxDecimals);
    }
}
