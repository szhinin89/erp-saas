namespace ERP.Application.Modules.Accounting.DTOs;

public sealed record ConfiguracionContableEmpresaDto(
    Guid? CuentaInventarioId,
    Guid? CuentaCostoVentaId,
    Guid? CuentaProveedoresId,
    Guid? CuentaVentasId,
    Guid? CuentaClientesId,
    Guid? CuentaIvaComprasId,
    Guid? CuentaIvaVentasId,
    Guid? CuentaEfectivoId,
    Guid? CuentaBancoId);
