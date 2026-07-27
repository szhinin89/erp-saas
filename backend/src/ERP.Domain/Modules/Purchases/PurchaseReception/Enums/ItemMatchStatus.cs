namespace ERP.Domain.Modules.Purchases.PurchaseReception.Enums;

/// <summary>
/// Estado de conciliación de una <see cref="Entities.PurchaseReceptionLine"/> contra el catálogo
/// de Items. <see cref="Pending"/> es el estado inicial cuando no hay ninguna sugerencia por
/// encima del umbral de similitud; <see cref="NeedsReview"/> cuando hay sugerencias pero ninguna
/// es un código de proveedor exacto; <see cref="AutoMatched"/> solo ocurre por código de proveedor
/// exacto al persistir la línea; <see cref="ManuallyMatched"/> cuando el usuario confirma —
/// individualmente o en lote.
/// </summary>
public enum ItemMatchStatus
{
    Pending = 0,
    NeedsReview = 1,
    AutoMatched = 2,
    ManuallyMatched = 3,
}
