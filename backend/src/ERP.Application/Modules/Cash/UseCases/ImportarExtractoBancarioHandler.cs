using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Cash.DTOs;
using ERP.Application.Common;
using ERP.Domain.Modules.Cash;
using ERP.Domain.Modules.Cash.Entities;
using ERP.Domain.Modules.Cash.Interfaces;

namespace ERP.Application.Modules.Cash.UseCases;

public sealed record ImportarExtractoBancarioCommand(
    Guid CuentaBancariaId,
    DateTime PeriodoDesde,
    DateTime PeriodoHasta,
    decimal SaldoInicialExtracto,
    decimal SaldoFinalExtracto,
    IReadOnlyList<MovimientoExtractoParseRow> Movimientos) : IRequest<Result<ExtractoBancarioDto>>;

public sealed class ImportarExtractoBancarioCommandHandler
    : IRequestHandler<ImportarExtractoBancarioCommand, Result<ExtractoBancarioDto>>
{
    private readonly ICajaRepository _caja;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IUnitOfWork _uow;

    public ImportarExtractoBancarioCommandHandler(
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

    public async Task<Result<ExtractoBancarioDto>> Handle(ImportarExtractoBancarioCommand cmd, CancellationToken ct)
    {
        var cuenta = await _caja.GetCuentaBancariaByIdAsync(cmd.CuentaBancariaId, ct);
        if (cuenta is null || !cuenta.IsActive)
            return Result<ExtractoBancarioDto>.Failure("Cuenta bancaria no encontrada o inactiva.");

        if (cmd.Movimientos.Count == 0)
            return Result<ExtractoBancarioDto>.Failure("No hay movimientos para importar.");

        decimal net = 0;
        foreach (var r in cmd.Movimientos)
        {
            if (r.Tipo.Equals("Credito", StringComparison.OrdinalIgnoreCase))
                net += r.Monto;
            else if (r.Tipo.Equals("Debito", StringComparison.OrdinalIgnoreCase))
                net -= r.Monto;
            else
                return Result<ExtractoBancarioDto>.Failure($"Tipo de movimiento no válido: {r.Tipo}");
        }

        var esperado = cmd.SaldoInicialExtracto + net;
        if (Math.Abs(esperado - cmd.SaldoFinalExtracto) > 0.05m)
        {
            return Result<ExtractoBancarioDto>.Failure(
                $"El saldo final del extracto ({cmd.SaldoFinalExtracto:F2}) no coincide con saldo inicial + movimientos ({esperado:F2}).");
        }

        var extracto = ExtractoBancario.Create(
            _tenant.TenantId,
            cmd.CuentaBancariaId,
            cmd.PeriodoDesde,
            cmd.PeriodoHasta,
            cmd.SaldoInicialExtracto,
            cmd.SaldoFinalExtracto,
            _user.UserId);

        foreach (var r in cmd.Movimientos.OrderBy(x => x.Fecha))
            extracto.AgregarMovimiento(r.Fecha, r.Descripcion, r.Monto, r.Tipo, r.Referencia, _user.UserId);

        await _caja.AddExtractoAsync(extracto, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<ExtractoBancarioDto>.Success(
            new ExtractoBancarioDto(
                extracto.Id,
                extracto.CuentaBancariaId,
                extracto.PeriodoDesde,
                extracto.PeriodoHasta,
                extracto.SaldoInicialExtracto,
                extracto.SaldoFinalExtracto,
                extracto.FechaCarga,
                extracto.Conciliado,
                extracto.Movimientos.Count));
    }
}
