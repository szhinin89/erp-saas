namespace ERP.Application.Modules.Pricing.DTOs;

/// <summary>Cliente asignado a una PriceList (identidad + nombre para mostrar) — sin datos de pricing.</summary>
public sealed record PriceListCustomerDto(
    Guid CustomerId,
    string CustomerName,
    string? CustomerIdentificationNumber
);

/// <summary>
/// PRICING-CUSTOMER-PRICE-LIST-ADMIN-05B — resultado de intentar asignar un cliente a una
/// PriceList. La regla de negocio "máximo 1 lista ACTIVA por cliente+empresa" nunca se rompe
/// con un error técnico (constraint de BD) — el conflicto se detecta ANTES de escribir y se
/// expone como <see cref="Conflict"/>, con los datos de la lista actual para que la UI pida
/// confirmación explícita antes de reintentar con <c>ConfirmSwitch: true</c>.
/// </summary>
public enum PriceListCustomerAssignStatus
{
    /// <summary>Se creó (o reactivó) la asignación — el cliente no tenía ninguna lista activa antes.</summary>
    Assigned,

    /// <summary>El cliente ya estaba asignado exactamente a esta misma lista — operación idempotente, sin cambios.</summary>
    AlreadyActive,

    /// <summary>
    /// El cliente ya tiene otra PriceList activa — no se escribió nada. La UI debe mostrar
    /// confirmación ("Este cliente usa [ConflictingPriceListName]. ¿Cambiarlo a [la nueva]?")
    /// y, si el usuario acepta, reenviar el comando con <c>ConfirmSwitch: true</c>.
    /// </summary>
    Conflict,

    /// <summary>Se desactivó la lista anterior y se activó/creó la nueva, en una sola transacción.</summary>
    Switched,
}

public sealed record PriceListCustomerAssignResultDto(
    PriceListCustomerAssignStatus Status,
    Guid? ConflictingPriceListId = null,
    string? ConflictingPriceListName = null
);
