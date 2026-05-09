using ERP.Application.Modules.Compras.UseCases.CrearCompra;
using ERP.Domain.Bodegas.Interfaces;

namespace ERP.Application.Modules.Compras;

/// <summary>Validación de distribución de cantidades de compra por bodega.</summary>
public static class CompraAsignacionBodegasRules
{
    public const decimal CantidadTolerance = 0.000001m;

    /// <returns>null si es válido; mensaje de error en caso contrario.</returns>
    public static async Task<string?> ValidateAsync(
        IReadOnlyList<DetalleCompraInput> detalles,
        IReadOnlyList<AsignacionBodegaRequest> asignaciones,
        Guid tenantId,
        IBodegaRepository bodegas,
        CancellationToken ct)
    {
        if (asignaciones.Count == 0)
            return "La lista de asignaciones no puede estar vacía si se envía el bloque.";

        foreach (var a in asignaciones)
        {
            if (a.ItemIndex < 0 || a.ItemIndex >= detalles.Count)
                return $"ItemIndex {a.ItemIndex} está fuera de rango (0..{detalles.Count - 1}).";

            if (a.Cantidad <= 0)
                return $"La cantidad asignada debe ser mayor a cero (ItemIndex {a.ItemIndex}).";

            var bodega = await bodegas.GetByIdAsync(tenantId, a.BodegaId, ct);
            if (bodega is null)
                return $"Bodega {a.BodegaId} no encontrada en el tenant.";
            if (!bodega.IsActive)
                return $"La bodega '{bodega.Nombre}' está deshabilitada.";
        }

        var sums = new decimal[detalles.Count];
        foreach (var a in asignaciones)
            sums[a.ItemIndex] += a.Cantidad;

        for (var i = 0; i < detalles.Count; i++)
        {
            var esperada = detalles[i].Cantidad;
            var suma   = sums[i];
            if (Math.Abs(suma - esperada) > CantidadTolerance)
                return $"La suma de cantidades asignadas al ítem {i} ({suma}) no coincide con la cantidad del detalle ({esperada}).";
        }

        return null;
    }

    /// <summary>Valida asignaciones contra líneas ya materializadas (mismo orden que al crear la compra).</summary>
    public static async Task<string?> ValidateAgainstDetallesAsync(
        IReadOnlyList<ERP.Domain.Compras.Entities.CompraDetalle> detallesOrdenados,
        IReadOnlyList<AsignacionBodegaRequest> asignaciones,
        Guid tenantId,
        IBodegaRepository bodegas,
        CancellationToken ct)
    {
        var inputs = detallesOrdenados
            .Select(d => new DetalleCompraInput(
                d.Descripcion,
                d.CodigoPrincipalProveedor,
                d.ProductoId,
                d.Cantidad,
                d.PrecioUnitario,
                d.DescuentoPorcentaje,
                d.IvaPorcentaje))
            .ToList();

        return await ValidateAsync(inputs, asignaciones, tenantId, bodegas, ct);
    }
}
