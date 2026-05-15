namespace ERP.Application.Modules.Purchasing.Services;

/// <summary>CÃ¡lculo de valor retenido segÃºn base y porcentaje (retenciones en la fuente).</summary>
public static class PurchaseRetentionCalculo
{
    public static decimal CalcularValorRetenido(decimal baseImponible, decimal porcentajeRetencion)
        => Math.Round(baseImponible * porcentajeRetencion / 100m, 2);
}
