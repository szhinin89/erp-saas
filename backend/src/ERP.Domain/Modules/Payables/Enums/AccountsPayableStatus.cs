namespace ERP.Domain.Modules.Payables.Enums;

/// <summary>
/// PAYABLES-GENERIC-FOUNDATION-09 — estado de la CxP (cabecera) y de cada cuota (mismo conjunto de
/// valores, reutilizado — una cuota es, en sí misma, una mini obligación). Pago/abono aún no están
/// implementados en esta fase: toda CxP/cuota nueva nace en <see cref="Pending"/>, y
/// <see cref="PartiallyPaid"/>/<see cref="Paid"/> quedan reservados para la fase de Pagos.
/// </summary>
public enum AccountsPayableStatus
{
    Pending,
    PartiallyPaid,
    Paid,
    Cancelled,
}
