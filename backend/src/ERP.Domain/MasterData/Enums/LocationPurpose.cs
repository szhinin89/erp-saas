namespace ERP.Domain.MasterData.Enums;

/// <summary>
/// Propósito de uso de una ubicación. Bitmask — una ubicación puede tener múltiples propósitos.
///
/// Ejemplo: Matriz = Billing | Fiscal | Correspondence (valor = 13).
/// </summary>
[Flags]
public enum LocationPurpose : int
{
    None           = 0,
    Billing        = 1,
    Delivery       = 2,
    Fiscal         = 4,
    Correspondence = 8,
}
