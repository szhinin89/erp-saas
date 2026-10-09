namespace ERP.Domain.Modules.Company.Entities;

/// <summary>
/// IL-5A — estado de la empresa que gobierna <see cref="Company.OpeningBalanceDate"/>.
/// <see cref="HasRealOperations"/>: existe al menos una operación real (venta/devolución autorizada,
/// compra/gasto confirmado, cobro/pago, sesión de caja o movimiento de inventario operativo); las
/// cargas iniciales son implementación y no cuentan. <see cref="ConfirmedOpeningDates"/>: fechas
/// de corte de las cargas iniciales ya confirmadas (inventario, CxC, …).
/// </summary>
public sealed record OpeningBalanceDateConstraints(
    bool HasRealOperations,
    IReadOnlyCollection<DateOnly> ConfirmedOpeningDates
);
