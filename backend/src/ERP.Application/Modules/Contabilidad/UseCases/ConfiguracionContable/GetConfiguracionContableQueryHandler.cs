using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Contabilidad.DTOs;
using ERP.Domain.Modules.Contabilidad.Interfaces;

namespace ERP.Application.Modules.Contabilidad.UseCases.ConfiguracionContable;

public sealed class GetConfiguracionContableQueryHandler
    : IRequestHandler<GetConfiguracionContableQuery, Result<ConfiguracionContableEmpresaDto?>>
{
    private readonly IConfiguracionContableRepository _repo;

    public GetConfiguracionContableQueryHandler(IConfiguracionContableRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<ConfiguracionContableEmpresaDto?>> Handle(
        GetConfiguracionContableQuery request,
        CancellationToken ct)
    {
        var e = await _repo.GetConfiguracionEmpresaAsync(ct);
        if (e is null)
            return Result<ConfiguracionContableEmpresaDto?>.Success(null);

        return Result<ConfiguracionContableEmpresaDto?>.Success(new ConfiguracionContableEmpresaDto(
            e.CuentaInventarioId,
            e.CuentaCostoVentaId,
            e.CuentaProveedoresId,
            e.CuentaVentasId,
            e.CuentaClientesId,
            e.CuentaIvaComprasId,
            e.CuentaIvaVentasId,
            e.CuentaEfectivoId,
            e.CuentaBancoId));
    }
}
