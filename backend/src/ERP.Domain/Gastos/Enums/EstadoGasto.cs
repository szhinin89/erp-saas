namespace ERP.Domain.Gastos.Enums;

/// <summary>Ciclo de vida de un comprobante de gasto.</summary>
public enum EstadoGasto
{
    Borrador  = 0,
    Validado  = 1,
    Aprobado  = 2,
    Rechazado = 3,
}
