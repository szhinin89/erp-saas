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
        CancellationToken ct
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
                    sort++
                )
            );
        }

        return Result<List<StockAdjustmentLine>>.Success(lines);
    }
}
