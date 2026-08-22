using ERP.Domain.Common;

namespace ERP.Domain.Modules.Inventory.Entities;

/// <summary>
/// Línea de ajuste de inventario. CompanyId heredado del StockAdjustment padre.
/// INVENTORY-ADJUSTMENTS-02 — reemplaza la forma anterior (SystemQuantity/PhysicalQuantity, un
/// diseño de reconciliación de conteo físico nunca poblado por ningún handler) por una línea que
/// captura Item + presentación (PackagingLevel) + cantidad, resuelta a unidad base como
/// <c>PurchaseInvoiceDetail</c>. EF Core popula las propiedades via reflection — setters private
/// son seguros.
/// </summary>
public sealed class StockAdjustmentLine : ITenantScopedEntity, ICompanyOperationalEntity
{
    public const int ItemNameMaxLen = 300;
    public const int UomCodeMaxLen = 10;
    public const int LineNotesMaxLen = 300;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid StockAdjustmentId { get; private set; }
    public Guid ItemId { get; private set; }
    public string ItemName { get; private set; } = null!;
    public Guid? PackagingLevelId { get; private set; }
    public string UomCode { get; private set; } = "UNIT";
    public string BaseUomCode { get; private set; } = "UNIT";
    public decimal ConversionFactor { get; private set; } = 1m;
    public decimal Quantity { get; private set; }
    public decimal QuantityInBaseUom { get; private set; }
    public decimal? UnitCostBase { get; private set; }
    public decimal? TotalCost { get; private set; }
    public decimal? CurrentStockBefore { get; private set; }
    public decimal? CurrentStockAfter { get; private set; }
    public string? LineNotes { get; private set; }
    public short SortOrder { get; private set; }

    public StockAdjustment StockAdjustment { get; private set; } = null!;

    private StockAdjustmentLine() { }

    public static StockAdjustmentLine Create(
        Guid tenantId,
        Guid companyId,
        Guid itemId,
        string itemName,
        Guid? packagingLevelId,
        string uomCode,
        string baseUomCode,
        decimal conversionFactor,
        decimal quantity,
        decimal? unitCostBase,
        string? lineNotes,
        short sortOrder,
        Guid stockAdjustmentId = default
    )
    {
        if (string.IsNullOrWhiteSpace(itemName))
            throw new ArgumentException("El nombre del ítem es obligatorio.", nameof(itemName));
        if (quantity <= 0)
            throw new ArgumentException("La cantidad debe ser mayor a cero.", nameof(quantity));
        if (conversionFactor <= 0)
            throw new ArgumentException(
                "El factor de conversión debe ser mayor a cero.",
                nameof(conversionFactor)
            );
        if (string.IsNullOrWhiteSpace(uomCode))
            throw new ArgumentException("La unidad de medida es obligatoria.", nameof(uomCode));
        if (string.IsNullOrWhiteSpace(baseUomCode))
            throw new ArgumentException(
                "La unidad base de inventario es obligatoria.",
                nameof(baseUomCode)
            );
        if (unitCostBase is < 0)
            throw new ArgumentException(
                "El costo unitario no puede ser negativo.",
                nameof(unitCostBase)
            );

        var quantityInBaseUom = Math.Round(
            quantity * conversionFactor,
            FiscalPrecision.Quantity,
            MidpointRounding.AwayFromZero
        );

        return new StockAdjustmentLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            StockAdjustmentId = stockAdjustmentId,
            ItemId = itemId,
            ItemName = itemName.Trim(),
            PackagingLevelId = packagingLevelId,
            UomCode = uomCode.Trim().ToUpperInvariant(),
            BaseUomCode = baseUomCode.Trim().ToUpperInvariant(),
            ConversionFactor = conversionFactor,
            Quantity = quantity,
            QuantityInBaseUom = quantityInBaseUom,
            UnitCostBase = unitCostBase,
            TotalCost = unitCostBase.HasValue
                ? Math.Round(
                    quantityInBaseUom * unitCostBase.Value,
                    FiscalPrecision.UnitCost,
                    MidpointRounding.AwayFromZero
                )
                : null,
            LineNotes = string.IsNullOrWhiteSpace(lineNotes) ? null : lineNotes.Trim(),
            SortOrder = sortOrder,
        };
    }

    /// <summary>
    /// Fija los saldos resueltos al momento de Execute (antes/después) y — para Ingreso — el costo
    /// unitario/costo total definitivamente aplicado al Kardex. Llamado exclusivamente por el
    /// handler de ExecuteStockAdjustment; la línea no conoce stock por sí misma.
    /// </summary>
    public void ApplyExecutionResult(
        decimal currentStockBefore,
        decimal currentStockAfter,
        decimal? resolvedUnitCostBase
    )
    {
        CurrentStockBefore = currentStockBefore;
        CurrentStockAfter = currentStockAfter;
        if (resolvedUnitCostBase.HasValue)
        {
            UnitCostBase = resolvedUnitCostBase;
            TotalCost = Math.Round(
                QuantityInBaseUom * resolvedUnitCostBase.Value,
                FiscalPrecision.UnitCost,
                MidpointRounding.AwayFromZero
            );
        }
    }
}
