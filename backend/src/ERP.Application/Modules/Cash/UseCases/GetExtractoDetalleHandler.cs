using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Cash.DTOs;
using ERP.Domain.Modules.Cash.Interfaces;

namespace ERP.Application.Modules.Cash.UseCases;

public sealed record ExtractoDetalleDto(
    ExtractoBancarioDto Cabecera,
    IReadOnlyList<MovimientoBancarioDto> Movimientos);

public sealed record GetExtractoDetalleQuery(Guid ExtractoId) : IRequest<Result<ExtractoDetalleDto>>;

public sealed class GetExtractoDetalleQueryHandler : IRequestHandler<GetExtractoDetalleQuery, Result<ExtractoDetalleDto>>
{
    private readonly ICajaRepository _caja;

    public GetExtractoDetalleQueryHandler(ICajaRepository caja) => _caja = caja;

    public async Task<Result<ExtractoDetalleDto>> Handle(GetExtractoDetalleQuery request, CancellationToken ct)
    {
        var x = await _caja.GetExtractoWithMovimientosAsync(request.ExtractoId, ct);
        if (x is null)
            return Result<ExtractoDetalleDto>.Failure("Extracto no encontrado.");

        var cab = new ExtractoBancarioDto(
            x.Id,
            x.CuentaBancariaId,
            x.PeriodoDesde,
            x.PeriodoHasta,
            x.SaldoInicialExtracto,
            x.SaldoFinalExtracto,
            x.FechaCarga,
            x.Conciliado,
            x.Movimientos.Count);

        var movs = x.Movimientos
            .OrderBy(m => m.Fecha)
            .Select(m => new MovimientoBancarioDto(
                m.Id,
                m.ExtractoBancarioId,
                m.Fecha,
                m.Descripcion,
                m.Monto,
                m.Tipo,
                m.Referencia,
                m.AsientoContableId,
                m.Estado))
            .ToList();

        return Result<ExtractoDetalleDto>.Success(new ExtractoDetalleDto(cab, movs));
    }
}
