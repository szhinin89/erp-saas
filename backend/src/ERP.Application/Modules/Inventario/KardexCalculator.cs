using ERP.Domain.Modules.Inventario.Entities;

namespace ERP.Application.Inventario;

/// <summary>
/// Lógica de acumulación del Kardex con promedio ponderado móvil.
/// Compartido entre el query handler (respuesta HTTP) y el worker nocturno (snapshots).
/// </summary>
public static class KardexCalculator
{
    /// <summary>
    /// Aplica un movimiento sobre el saldo acumulado (in-place vía ref).
    /// Entradas (Cantidad &gt; 0): recalculan el costo promedio.
    /// Salidas  (Cantidad &lt; 0): usan el promedio vigente; el promedio no cambia.
    /// </summary>
    public static void AplicarMovimiento(
        InventarioMovimiento m,
        ref decimal saldoCantidad,
        ref decimal saldoValor,
        ref decimal costoPromedio)
    {
        if (m.Cantidad > 0)
        {
            var costo = m.CostoUnitario ?? 0m;
            saldoValor    += m.Cantidad * costo;
            saldoCantidad += m.Cantidad;
            costoPromedio  = saldoCantidad > 0m ? saldoValor / saldoCantidad : 0m;
        }
        else
        {
            var salida = -m.Cantidad;
            saldoValor    -= salida * costoPromedio;
            saldoCantidad -= salida;
            if (saldoValor < 0m) saldoValor = 0m; // guardia frente a errores de redondeo
        }
    }
}
