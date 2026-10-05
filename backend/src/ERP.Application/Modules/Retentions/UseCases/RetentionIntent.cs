using ERP.Domain.Common;
using ERP.Domain.Modules.Retentions.Enums;
using FluentValidation;

namespace ERP.Application.Modules.Retentions.UseCases;

/// <summary>
/// Línea de una retención a emitir. El monto/porcentaje/base los propone el cliente (en Compras
/// precargados desde la vista previa, en Gastos escritos por el usuario); lo que el servidor SÍ
/// revalida siempre es la ELEGIBILIDAD (<c>IRetentionEligibilityService</c> dentro de
/// <c>RetentionIssuer</c>) y las reglas de forma de <see cref="IssueRetentionLineValidator"/>.
/// </summary>
public sealed record IssueRetentionLineInput(
    RetentionTaxType TaxType,
    string RetentionCode,
    decimal BaseAmount,
    decimal RetentionRate,
    decimal RetainedAmount,
    string? Description = null,
    // RETENTIONS-TAX-COMPONENT-MODEL-02B: snapshot requerido a nivel de dominio
    // (RetentionDocumentLine.RetentionCodeDescription), pero OPCIONAL aquí — si viene null/vacío,
    // RetentionIssuer usa RetentionCode como descripción de respaldo.
    string? RetentionCodeDescription = null
);

public sealed class IssueRetentionLineValidator : AbstractValidator<IssueRetentionLineInput>
{
    public IssueRetentionLineValidator()
    {
        RuleFor(x => x.RetentionCode).NotEmpty();
        RuleFor(x => x.BaseAmount).GreaterThan(0);
        RuleFor(x => x.RetentionRate).GreaterThan(0);
        // ZH-DESIGN-SYSTEM-PRECISION-04C1 — el % de retención es un porcentaje fiscal de escala fija
        // (FiscalPrecision.Percentage): un valor con más decimales se rechaza aquí en vez de que el
        // dominio lo redondee en silencio (RetentionDocumentLine.Create conserva su redondeo defensivo).
        RuleFor(x => x.RetentionRate)
            .Must(rate => decimal.Round(rate, FiscalPrecision.Percentage) == rate)
            .WithMessage(
                $"El porcentaje de retención admite como máximo {FiscalPrecision.Percentage} decimales."
            );
        RuleFor(x => x.RetainedAmount).GreaterThan(0);
        RuleFor(x => x)
            .Must(l => l.RetainedAmount <= l.BaseAmount)
            .WithMessage("El monto retenido no puede ser mayor a la base imponible.");
    }
}

/// <summary>
/// Intención OPCIONAL de emitir la retención dentro de la confirmación del documento origen —
/// contrato único para Gastos (<c>ConfirmExpenseDocumentCommand</c>/<c>CreateConfirmedExpenseCommand</c>)
/// y Compras (<c>ConfirmPurchaseCommand</c>), <c>docs/decisions/RETENTIONS-MODULE-DESIGN-01.md</c>
/// decisión 15 (ZH-PURCHASE-RETENTION-CONFIRM-01). <see cref="AppliesRetention"/> es solo la
/// intención, nunca prueba de que aplica: <c>RetentionIssuer</c> revalida la elegibilidad contra el
/// documento real antes de emitir. Nunca incluye Tenant/Company/Branch (salen del contexto
/// autenticado) ni un número de retención (RETENTIONS-DOCUMENT-SEQUENCE-02E: se genera vía
/// <c>CaptureNextAsync</c> a partir de <see cref="EmissionPointId"/>).
/// </summary>
public sealed record RetentionIntent(
    bool AppliesRetention,
    Guid? EmissionPointId,
    DateOnly? IssueDate,
    IReadOnlyList<IssueRetentionLineInput>? Lines
);

/// <summary>Reglas de <see cref="RetentionIntent"/> solo cuando <c>AppliesRetention == true</c> — compartidas por toda confirmación que acepta retención.</summary>
public sealed class RetentionIntentValidator : AbstractValidator<RetentionIntent>
{
    public RetentionIntentValidator()
    {
        When(
            x => x.AppliesRetention,
            () =>
            {
                RuleFor(x => x.EmissionPointId)
                    .Must(v => v.HasValue && v.Value != Guid.Empty)
                    .WithMessage("El punto de emisión es obligatorio para generar la retención.");
                RuleFor(x => x.IssueDate)
                    .NotEmpty()
                    .WithMessage("La fecha de emisión de la retención es obligatoria.");
                RuleFor(x => x.Lines)
                    .NotEmpty()
                    .WithMessage("Debe incluir al menos una línea de retención.");
                RuleForEach(x => x.Lines!).SetValidator(new IssueRetentionLineValidator());
            }
        );
    }
}
