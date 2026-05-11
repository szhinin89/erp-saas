using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Contabilidad.DTOs;

namespace ERP.Application.Modules.Contabilidad.UseCases.ConfiguracionContable;

[RequireFeature(SubscriptionFeatureCodes.Accounting)]
public sealed record UpsertConfiguracionContableCommand(
    Guid? CuentaInventarioId,
    Guid? CuentaCostoVentaId,
    Guid? CuentaProveedoresId,
    Guid? CuentaVentasId,
    Guid? CuentaClientesId,
    Guid? CuentaIvaComprasId,
    Guid? CuentaIvaVentasId,
    Guid? CuentaEfectivoId,
    Guid? CuentaBancoId
) : IRequest<Result<ConfiguracionContableEmpresaDto>>;
