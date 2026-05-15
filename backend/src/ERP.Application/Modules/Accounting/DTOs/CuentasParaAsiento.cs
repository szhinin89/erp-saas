namespace ERP.Application.Modules.Accounting.DTOs;

/// <summary>Cuentas resueltas para armar un asiento (compra o venta).</summary>
public sealed record CuentasParaAsiento(
    Guid CuentaDebitoPrincipal,
    Guid CuentaCreditoPrincipal,
    Guid? CuentaIvaDebito,
    Guid? CuentaIvaCredito);
