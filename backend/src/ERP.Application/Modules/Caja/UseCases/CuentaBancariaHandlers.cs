using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Caja.DTOs;
using ERP.Application.Common;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;

namespace ERP.Application.Modules.Caja.UseCases;

public sealed record ListCuentasBancariasQuery : IRequest<Result<IReadOnlyList<CuentaBancariaDto>>>;

public sealed class ListCuentasBancariasQueryHandler
    : IRequestHandler<ListCuentasBancariasQuery, Result<IReadOnlyList<CuentaBancariaDto>>>
{
    private readonly ICajaRepository _caja;

    public ListCuentasBancariasQueryHandler(ICajaRepository caja) => _caja = caja;

    public async Task<Result<IReadOnlyList<CuentaBancariaDto>>> Handle(
        ListCuentasBancariasQuery request,
        CancellationToken ct)
    {
        var list = await _caja.ListCuentasBancariasAsync(ct);
        return Result<IReadOnlyList<CuentaBancariaDto>>.Success(
            list.Select(x => new CuentaBancariaDto(
                x.Id,
                x.Nombre,
                x.NumeroCuenta,
                x.TipoCuenta,
                x.Moneda,
                x.SaldoInicial,
                x.SaldoActual,
                x.IsActive,
                x.CuentaContableId)).ToList());
    }
}

public sealed record CrearCuentaBancariaCommand(
    string Nombre,
    string NumeroCuenta,
    string TipoCuenta,
    string Moneda,
    decimal SaldoInicial,
    Guid? CuentaContableId) : IRequest<Result<CuentaBancariaDto>>;

public sealed class CrearCuentaBancariaCommandHandler
    : IRequestHandler<CrearCuentaBancariaCommand, Result<CuentaBancariaDto>>
{
    private readonly ICajaRepository _caja;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IUnitOfWork _uow;

    public CrearCuentaBancariaCommandHandler(
        ICajaRepository caja,
        ICurrentTenant tenant,
        ICurrentUser user,
        IUnitOfWork uow)
    {
        _caja   = caja;
        _tenant = tenant;
        _user   = user;
        _uow    = uow;
    }

    public async Task<Result<CuentaBancariaDto>> Handle(CrearCuentaBancariaCommand cmd, CancellationToken ct)
    {
        var entity = CuentaBancaria.Create(
            _tenant.TenantId,
            cmd.Nombre,
            cmd.NumeroCuenta,
            cmd.TipoCuenta,
            cmd.Moneda,
            cmd.SaldoInicial,
            _user.UserId,
            cmd.CuentaContableId);
        await _caja.AddCuentaBancariaAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<CuentaBancariaDto>.Success(
            new CuentaBancariaDto(
                entity.Id,
                entity.Nombre,
                entity.NumeroCuenta,
                entity.TipoCuenta,
                entity.Moneda,
                entity.SaldoInicial,
                entity.SaldoActual,
                entity.IsActive,
                entity.CuentaContableId));
    }
}

public sealed record ListExtractosPorCuentaQuery(Guid CuentaBancariaId)
    : IRequest<Result<IReadOnlyList<ExtractoBancarioDto>>>;

public sealed class ListExtractosPorCuentaQueryHandler
    : IRequestHandler<ListExtractosPorCuentaQuery, Result<IReadOnlyList<ExtractoBancarioDto>>>
{
    private readonly ICajaRepository _caja;

    public ListExtractosPorCuentaQueryHandler(ICajaRepository caja) => _caja = caja;

    public async Task<Result<IReadOnlyList<ExtractoBancarioDto>>> Handle(
        ListExtractosPorCuentaQuery request,
        CancellationToken ct)
    {
        var list = await _caja.ListExtractosByCuentaAsync(request.CuentaBancariaId, ct);
        return Result<IReadOnlyList<ExtractoBancarioDto>>.Success(
            list.Select(x => new ExtractoBancarioDto(
                x.Id,
                x.CuentaBancariaId,
                x.PeriodoDesde,
                x.PeriodoHasta,
                x.SaldoInicialExtracto,
                x.SaldoFinalExtracto,
                x.FechaCarga,
                x.Conciliado,
                x.Movimientos.Count)).ToList());
    }
}
