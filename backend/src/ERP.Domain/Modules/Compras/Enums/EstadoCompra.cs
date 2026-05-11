namespace ERP.Domain.Modules.Compras.Enums;

/// <summary>
/// Ciclo de vida de una CompraFactura:
/// Borrador → Validado → Aprobado
///                    ↘ Rechazado
/// </summary>
public enum EstadoCompra
{
    Borrador  = 0,
    Validado  = 1,
    Aprobado  = 2,
    Rechazado = 3,
}
