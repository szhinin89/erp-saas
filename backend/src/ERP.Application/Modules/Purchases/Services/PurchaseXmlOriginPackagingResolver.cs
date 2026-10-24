using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Modules.Items.Entities;

namespace ERP.Application.Modules.Purchases.Services;

/// <summary>
/// FASE 5 PURCHASE-UX — resuelve la presentación de origen XML de una línea: nivel del propio ítem
/// (activo o no — es un snapshot de interpretación, no la presentación operativa) y su factor. Si la
/// línea ya tenía persistido el mismo nivel, conserva el factor snapshot anterior.
/// </summary>
public static class PurchaseXmlOriginPackagingResolver
{
    public static (Guid? LevelId, decimal? Factor, string? Error) Resolve(
        Item? item,
        PurchaseLineInput line,
        int conversionFactorDecimals,
        (Guid LevelId, decimal Factor)? previous
    )
    {
        if (line.XmlAllocation is not null || line.XmlOriginPackagingLevelId is not { } levelId)
            return (null, null, null);
        // Solo líneas de Recepción con ítem: en el resto el origen no aplica y se ignora.
        if (line.PurchaseReceptionLineId is null || item is null)
            return (null, null, null);
        if (previous is { } prev && prev.LevelId == levelId)
            return (levelId, prev.Factor, null);

        var level = item.PackagingLevels.FirstOrDefault(p => p.Id == levelId);
        if (level is null)
            return (
                null,
                null,
                $"Línea '{line.Description}': la presentación de origen XML no pertenece al ítem."
            );
        var factor = Math.Round(
            level.BaseQuantity,
            conversionFactorDecimals,
            MidpointRounding.AwayFromZero
        );
        if (factor <= 0m)
            return (
                null,
                null,
                $"Línea '{line.Description}': el factor de la presentación de origen XML no es representable con {conversionFactorDecimals} decimales."
            );
        return (levelId, factor, null);
    }
}
