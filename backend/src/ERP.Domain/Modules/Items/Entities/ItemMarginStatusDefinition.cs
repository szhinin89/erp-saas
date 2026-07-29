namespace ERP.Domain.Modules.Items.Entities;

/// <summary>
/// Catálogo global de solo lectura de estados de margen de rentabilidad de ítems
/// (SALUDABLE, BAJO, NEGATIVO, CERO, SIN_PRECIO). Referencia consultable por API para que
/// el frontend deje de hardcodear el mapeo label/color. El cálculo de negocio real vive en
/// <c>ItemProfitabilityUseCases.CalcMargin</c> y no depende de este catálogo.
/// </summary>
public class ItemMarginStatusDefinition
{
    public string Code { get; set; } = null!;
    public string Label { get; set; } = null!;
    public string ColorToken { get; set; } = null!;
}
