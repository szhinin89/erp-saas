namespace ERP.Domain.Modules.Retentions.Enums;

/// <summary>
/// Tipo de impuesto retenido por una <c>RetentionDocumentLine</c>. ISD deliberadamente omitido —
/// <c>RetentionCalculator</c> tampoco lo calcula hoy (<c>TotalRetainedIsd = 0</c> siempre), ver
/// <c>docs/decisions/RETENTIONS-MODULE-DESIGN-01.md</c>.
/// </summary>
public enum RetentionTaxType
{
    Vat = 1,
    Income = 2,
}
