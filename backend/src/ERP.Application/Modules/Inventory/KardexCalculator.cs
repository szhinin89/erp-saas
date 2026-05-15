using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Application.Inventory;

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
    public static void ApplyMovement(
        StockMovement m,
        ref decimal saldoCantidad,
        ref decimal saldoValor,
        ref decimal costoPromedio)
    {
        if (m.Quantity > 0)
        {
            var costo = m.UnitCost ?? 0m;
            saldoValor    += m.Quantity * costo;
            saldoCantidad += m.Quantity;
            costoPromedio  = saldoCantidad > 0m ? saldoValor / saldoCantidad : 0m;
        }
        else
        {
            var salida = -m.Quantity;
            saldoValor    -= salida * costoPromedio;
            saldoCantidad -= salida;
            if (saldoValor < 0m) saldoValor = 0m; // guardia frente a errores de redondeo
        }
    }
}
