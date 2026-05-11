namespace ERP.Application.Modules.Compras.Services;

/// <summary>Cálculo de valor retenido según base y porcentaje (retenciones en la fuente).</summary>
public static class CompraRetencionCalculo
{
    public static decimal CalcularValorRetenido(decimal baseImponible, decimal porcentajeRetencion)
        => Math.Round(baseImponible * porcentajeRetencion / 100m, 2);
}
