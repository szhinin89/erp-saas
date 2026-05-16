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
    /// Inflows (Cantidad &gt; 0): recalculan el costo promedio.
    /// Outflows  (Cantidad &lt; 0): usan el promedio vigente; el promedio no cambia.
    /// </summary>
    public static void ApplyMovement(
        StockMovement m,
        ref decimal balanceQuantity,
        ref decimal balanceValue,
        ref decimal averageCost)
    {
        if (m.Quantity > 0)
        {
            var costo = m.UnitCost ?? 0m;
            balanceValue    += m.Quantity * costo;
            balanceQuantity += m.Quantity;
            averageCost  = balanceQuantity > 0m ? balanceValue / balanceQuantity : 0m;
        }
        else
        {
            var salida = -m.Quantity;
            balanceValue    -= salida * averageCost;
            balanceQuantity -= salida;
            if (balanceValue < 0m) balanceValue = 0m; // guardia frente a errores de redondeo
        }
    }
}
