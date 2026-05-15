using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Cash.DTOs;
using ERP.Application.Common;
using ERP.Domain.Modules.Cash.Entities;
using ERP.Domain.Modules.Cash.Interfaces;

namespace ERP.Application.Modules.Cash.UseCases;

public sealed record ListCajasChicasQuery : IRequest<Result<IReadOnlyList<CajaChicaDto>>>;

public sealed class ListCajasChicasQueryHandler : IRequestHandler<ListCajasChicasQuery, Result<IReadOnlyList<CajaChicaDto>>>
{
    private readonly ICajaRepository _caja;
    public ListCajasChicasQueryHandler(ICajaRepository caja) => _caja = caja;

    public async Task<Result<IReadOnlyList<CajaChicaDto>>> Handle(ListCajasChicasQuery _, CancellationToken ct)
    {
        var list = await _caja.ListCajasChicasAsync(ct);
        return Result<IReadOnlyList<CajaChicaDto>>.Success(
            list.Select(x => new CajaChicaDto(
                x.Id,
                x.Nombre,
                x.SaldoAsignado,
                x.SaldoActual,
                x.CuentaBancariaIdReposicion,
                x.CuentaContableCajaId,
                x.IsActive)).ToList());
    }
}

public sealed record CrearCajaChicaCommand(
    string Nombre,
    decimal SaldoAsignado,
    Guid? CuentaBancariaIdReposicion,
    Guid? CuentaContableCajaId) : IRequest<Result<CajaChicaDto>>;

public sealed class CrearCajaChicaCommandHandler : IRequestHandler<CrearCajaChicaCommand, Result<CajaChicaDto>>
{
    private readonly ICajaRepository _caja;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IUnitOfWork _uow;

    public CrearCajaChicaCommandHandler(ICajaRepository caja, ICurrentTenant tenant, ICurrentUser user, IUnitOfWork uow)
    {
        _caja   = caja;
        _tenant = tenant;
        _user   = user;
        _uow    = uow;
    }

    public async Task<Result<CajaChicaDto>> Handle(CrearCajaChicaCommand cmd, CancellationToken ct)
    {
        var c = CajaChica.Create(
            _tenant.TenantId,
            cmd.Nombre,
            cmd.SaldoAsignado,
            _user.UserId,
            cmd.CuentaBancariaIdReposicion,
            cmd.CuentaContableCajaId);
        await _caja.AddCajaChicaAsync(c, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<CajaChicaDto>.Success(
            new CajaChicaDto(
                c.Id,
                c.Nombre,
                c.SaldoAsignado,
                c.SaldoActual,
                c.CuentaBancariaIdReposicion,
                c.CuentaContableCajaId,
                c.IsActive));
    }
}

public sealed record CrearGastoCajaChicaCommand(
    Guid CajaChicaId,
    DateTime Fecha,
    string Concepto,
    decimal Monto,
    string TipoComprobante,
    string? NumeroComprobante) : IRequest<Result<GastoCajaChicaDto>>;

public sealed class CrearGastoCajaChicaCommandHandler
    : IRequestHandler<CrearGastoCajaChicaCommand, Result<GastoCajaChicaDto>>
{
    private readonly ICajaRepository _caja;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IUnitOfWork _uow;

    public CrearGastoCajaChicaCommandHandler(
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

    public async Task<Result<GastoCajaChicaDto>> Handle(CrearGastoCajaChicaCommand cmd, CancellationToken ct)
    {
        var caja = await _caja.GetCajaChicaByIdAsync(cmd.CajaChicaId, ct);
        if (caja is null || !caja.IsActive)
            return Result<GastoCajaChicaDto>.Failure("Caja chica no encontrada o inactiva.");

        try
        {
            caja.RegistrarGasto(cmd.Monto, _user.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<GastoCajaChicaDto>.Failure(ex.Message);
        }

        var gasto = GastoCajaChica.Create(
            _tenant.TenantId,
            cmd.CajaChicaId,
            cmd.Fecha,
            cmd.Concepto,
            cmd.Monto,
            cmd.TipoComprobante,
            cmd.NumeroComprobante,
            _user.UserId);

        await _caja.AddGastoCajaAsync(gasto, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<GastoCajaChicaDto>.Success(
            new GastoCajaChicaDto(
                gasto.Id,
                gasto.CajaChicaId,
                gasto.Fecha,
                gasto.Concepto,
                gasto.Monto,
                gasto.TipoComprobante,
                gasto.NumeroComprobante,
                gasto.AsientoContableId));
    }
}

public sealed record CrearArqueoCajaCommand(
    Guid CajaChicaId,
    DateTime FechaArqueo,
    decimal EfectivoFisico,
    string? Observaciones) : IRequest<Result<ArqueoCajaDto>>;

public sealed class CrearArqueoCajaCommandHandler : IRequestHandler<CrearArqueoCajaCommand, Result<ArqueoCajaDto>>
{
    private readonly ICajaRepository _caja;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IUnitOfWork _uow;

    public CrearArqueoCajaCommandHandler(ICajaRepository caja, ICurrentTenant tenant, ICurrentUser user, IUnitOfWork uow)
    {
        _caja   = caja;
        _tenant = tenant;
        _user   = user;
        _uow    = uow;
    }

    public async Task<Result<ArqueoCajaDto>> Handle(CrearArqueoCajaCommand cmd, CancellationToken ct)
    {
        var caja = await _caja.GetCajaChicaByIdAsync(cmd.CajaChicaId, ct);
        if (caja is null)
            return Result<ArqueoCajaDto>.Failure("Caja chica no encontrada.");

        var arqueo = ArqueoCaja.Create(
            _tenant.TenantId,
            cmd.CajaChicaId,
            cmd.FechaArqueo,
            cmd.EfectivoFisico,
            caja.SaldoActual,
            cmd.Observaciones,
            _user.UserId);

        await _caja.AddArqueoAsync(arqueo, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<ArqueoCajaDto>.Success(
            new ArqueoCajaDto(
                arqueo.Id,
                arqueo.CajaChicaId,
                arqueo.FechaArqueo,
                arqueo.EfectivoFisico,
                arqueo.Diferencia,
                arqueo.Observaciones,
                arqueo.Aprobado));
    }
}

public sealed record AprobarArqueoCajaCommand(Guid ArqueoId) : IRequest<Result<ArqueoCajaDto>>;

public sealed class AprobarArqueoCajaCommandHandler : IRequestHandler<AprobarArqueoCajaCommand, Result<ArqueoCajaDto>>
{
    private readonly ICajaRepository _caja;
    private readonly ICurrentUser _user;
    private readonly IUnitOfWork _uow;

    public AprobarArqueoCajaCommandHandler(ICajaRepository caja, ICurrentUser user, IUnitOfWork uow)
    {
        _caja = caja;
        _user = user;
        _uow  = uow;
    }

    public async Task<Result<ArqueoCajaDto>> Handle(AprobarArqueoCajaCommand cmd, CancellationToken ct)
    {
        var arqueo = await _caja.GetArqueoByIdAsync(cmd.ArqueoId, ct);
        if (arqueo is null)
            return Result<ArqueoCajaDto>.Failure("Arqueo no encontrado.");

        var caja = await _caja.GetCajaChicaByIdAsync(arqueo.CajaChicaId, ct);
        if (caja is null)
            return Result<ArqueoCajaDto>.Failure("Caja chica no encontrada.");

        try
        {
            arqueo.Aprobar(_user.UserId);
            caja.AplicarSaldoTrasArqueo(arqueo.EfectivoFisico, _user.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<ArqueoCajaDto>.Failure(ex.Message);
        }

        await _uow.SaveChangesAsync(ct);

        return Result<ArqueoCajaDto>.Success(
            new ArqueoCajaDto(
                arqueo.Id,
                arqueo.CajaChicaId,
                arqueo.FechaArqueo,
                arqueo.EfectivoFisico,
                arqueo.Diferencia,
                arqueo.Observaciones,
                arqueo.Aprobado));
    }
}

public sealed record ReposicionCajaChicaCommand(Guid CajaChicaId, decimal Monto) : IRequest<Result<CajaChicaDto>>;

public sealed class ReposicionCajaChicaCommandHandler
    : IRequestHandler<ReposicionCajaChicaCommand, Result<CajaChicaDto>>
{
    private readonly ICajaRepository _caja;
    private readonly ICurrentUser _user;
    private readonly IUnitOfWork _uow;

    public ReposicionCajaChicaCommandHandler(ICajaRepository caja, ICurrentUser user, IUnitOfWork uow)
    {
        _caja = caja;
        _user = user;
        _uow  = uow;
    }

    public async Task<Result<CajaChicaDto>> Handle(ReposicionCajaChicaCommand cmd, CancellationToken ct)
    {
        var caja = await _caja.GetCajaChicaByIdAsync(cmd.CajaChicaId, ct);
        if (caja is null || !caja.IsActive)
            return Result<CajaChicaDto>.Failure("Caja chica no encontrada o inactiva.");

        if (caja.CuentaBancariaIdReposicion is null)
            return Result<CajaChicaDto>.Failure(
                "La caja chica no tiene cuenta bancaria de reposición configurada.");

        try
        {
            caja.RegistrarReposicion(cmd.Monto, _user.UserId);
        }
        catch (ArgumentException ex)
        {
            return Result<CajaChicaDto>.Failure(ex.Message);
        }

        var banco = await _caja.GetCuentaBancariaByIdAsync(caja.CuentaBancariaIdReposicion.Value, ct);
        if (banco is not null)
            banco.IncrementarSaldo(-cmd.Monto, _user.UserId);

        await _uow.SaveChangesAsync(ct);

        return Result<CajaChicaDto>.Success(
            new CajaChicaDto(
                caja.Id,
                caja.Nombre,
                caja.SaldoAsignado,
                caja.SaldoActual,
                caja.CuentaBancariaIdReposicion,
                caja.CuentaContableCajaId,
                caja.IsActive));
    }
}
