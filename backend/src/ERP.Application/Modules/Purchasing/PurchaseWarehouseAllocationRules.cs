using ERP.Application.Modules.Purchasing.UseCases.CreatePurchase;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Modules.Purchasing;

/// <summary>Validación de distribución de cantidades de compra por Warehouse.</summary>
public static class PurchaseAsignacionWarehousesRules
{
    public const decimal CantidadTolerance = 0.000001m;

    /// <returns>null si es válido; mensaje de error en caso contrario.</returns>
    public static async Task<string?> ValidateAsync(
        IReadOnlyList<PurchaseLineInput> lines,
        IReadOnlyList<WarehouseAllocationRequest> asignaciones,
        Guid subscriberId,
        IWarehouseRepository bodegas,
        CancellationToken ct)
    {
        if (asignaciones.Count == 0)
            return "La lista de asignaciones no puede estar vacía si se envía el bloque.";

        foreach (var a in asignaciones)
        {
            if (a.ItemIndex < 0 || a.ItemIndex >= lines.Count)
                return $"ItemIndex {a.ItemIndex} está fuera de rango (0..{lines.Count - 1}).";

            if (a.Quantity <= 0)
                return $"La cantidad asignada debe ser mayor a cero (ItemIndex {a.ItemIndex}).";

            var Warehouse = await bodegas.GetByIdAsync(subscriberId, a.WarehouseId, ct);
            if (Warehouse is null)
                return $"Warehouse {a.WarehouseId} no encontrada en el tenant.";
            if (!Warehouse.IsActive)
                return $"La Warehouse '{Warehouse.Name}' está deshabilitada.";
        }

        var sums = new decimal[lines.Count];
        foreach (var a in asignaciones)
            sums[a.ItemIndex] += a.Quantity;

        for (var i = 0; i < lines.Count; i++)
        {
            var esperada = lines[i].Quantity;
            var suma   = sums[i];
            if (Math.Abs(suma - esperada) > CantidadTolerance)
                return $"La suma de cantidades asignadas al ítem {i} ({suma}) no coincide con la cantidad del line ({esperada}).";
        }

        return null;
    }

    /// <summary>Valida asignaciones contra líneas ya materializadas (mismo orden que al crear la compra).</summary>
    public static async Task<string?> ValidateAgainstDetallesAsync(
        IReadOnlyList<ERP.Domain.Modules.Purchasing.Entities.PurchBillLine> detallesOrdenados,
        IReadOnlyList<WarehouseAllocationRequest> asignaciones,
        Guid subscriberId,
        IWarehouseRepository bodegas,
        CancellationToken ct)
    {
        var inputs = detallesOrdenados
            .Select(d => new PurchaseLineInput(
                d.Description,
                d.SupplierProductCode,
                d.ProductId,
                d.Quantity,
                d.UnitPrice,
                d.DiscountPct,
                d.VatPct))
            .ToList();

        return await ValidateAsync(inputs, asignaciones, subscriberId, bodegas, ct);
    }
}
