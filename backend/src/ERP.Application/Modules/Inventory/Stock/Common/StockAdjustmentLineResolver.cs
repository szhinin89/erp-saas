using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.UseCases.CreateStockAdjustment;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Items.Interfaces;

namespace ERP.Application.Modules.Inventory.Stock.Common;

/// <summary>
/// Resuelve líneas de ajuste de inventario (Item + presentación → UomCode/BaseUomCode/
/// ConversionFactor/QuantityInBaseUom), mismo patrón que <c>PurchaseInvoiceDetail.Create</c>.
/// Compartido por Create/UpdateStockAdjustment para no duplicar la lógica de resolución.
/// </summary>
internal sealed class StockAdjustmentLineResolver
{
    private readonly IItemRepository _itemRepo;

    public StockAdjustmentLineResolver(IItemRepository itemRepo) => _itemRepo = itemRepo;

    public async Task<Result<List<StockAdjustmentLine>>> ResolveAsync(
        Guid tenantId,
        Guid companyId,
        IReadOnlyList<CreateStockAdjustmentLineInput> inputs,
        CancellationToken ct,
        // INVENTORY-ADJUSTMENTS-04 — bug encontrado en la validación e2e: al no pasar el
        // StockAdjustmentId real cuando ya se conoce (UpdateStockAdjustmentCommandHandler, donde el
        // agregado padre YA existe con un Id real), StockAdjustmentLine.Create() dejaba el FK en su
        // default (Guid.Empty). Para CreateStockAdjustmentCommandHandler eso es inofensivo — el
        // padre se agrega como Added y todo el grafo se inserta en cascada sin importar el valor
        // "original" de ningún hijo. Pero para Update, el padre ya está trackeado (Modified, no
        // Added): EF Core descubre la línea nueva por fixup de navegación y, como
        // StockAdjustmentId cambia de Guid.Empty (su snapshot "original" implícito) al Id real del
        // padre, lo detecta como una diferencia GENUINA de valores — exactamente lo que
        // NewChildEntityTrackingInterceptor está diseñado a NO auto-corregir (para no enmascarar
        // bugs reales), así que la línea queda mal clasificada Modified en vez de Added y EF emite
        // un UPDATE contra una fila que nunca existió → DbUpdateConcurrencyException ("0 rows
        // affected") → 409 en cada intento de reemplazar las líneas de un ajuste ya persistido.
        // Pasar el StockAdjustmentId real desde el inicio (cuando se conoce) elimina la diferencia
        // espuria de raíz.
        Guid stockAdjustmentId = default
    )
    {
        var lines = new List<StockAdjustmentLine>();
        short sort = 0;
        foreach (var input in inputs)
        {
            var item = await _itemRepo.GetByIdAsync(input.ItemId, tenantId, ct);
            if (item is null)
                return Result<List<StockAdjustmentLine>>.ValidationFailure(
                    $"El ítem '{input.ItemName}' no existe."
                );

            string uomCode;
            var baseUomCode = item.DefaultUomCode;
            decimal conversionFactor;

            if (input.PackagingLevelId.HasValue)
            {
                var level = item.PackagingLevels.FirstOrDefault(p =>
                    p.Id == input.PackagingLevelId.Value
                );
                if (level is null || !level.IsActive)
                    return Result<List<StockAdjustmentLine>>.ValidationFailure(
                        $"La presentación seleccionada para '{item.DefaultUomCode}' no es válida o está inactiva."
                    );
                uomCode = level.UomCode;
                conversionFactor = level.BaseQuantity;
            }
            else
            {
                uomCode = baseUomCode;
                conversionFactor = 1m;
            }

            lines.Add(
                StockAdjustmentLine.Create(
                    tenantId,
                    companyId,
                    input.ItemId,
                    input.ItemName,
                    input.PackagingLevelId,
                    uomCode,
                    baseUomCode,
                    conversionFactor,
                    input.Quantity,
                    input.UnitCostBase,
                    input.LineNotes,
                    sort++,
                    stockAdjustmentId
                )
            );
        }

        return Result<List<StockAdjustmentLine>>.Success(lines);
    }
}
