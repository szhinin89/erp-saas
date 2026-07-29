namespace ERP.Domain.Common;

/// <summary>
/// Escalas decimales frozen del Estándar de Precisión Numérica (CLAUDE.md, congelado 2026-06-25).
/// Deben coincidir siempre con la escala física de columna PostgreSQL de cada campo consumidor.
/// </summary>
public static class FiscalPrecision
{
    public const int TaxAmount = 2; // numeric(18,2): VatAmount, IceAmount, TaxableBase, TaxInclusiveTotal
    public const int Quantity = 4; // numeric(18,4): QuantityInBaseUom
    public const int UnitCost = 6; // numeric(18,6): DiscountAmount, TotalLineCost, LandedUnitCost
    public const int Percentage = 2; // numeric(5,2)
}
