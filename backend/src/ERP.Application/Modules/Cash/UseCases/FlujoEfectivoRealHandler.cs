using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Cash.DTOs;
using ERP.Application.Common;
using ERP.Domain.Modules.Cash.Interfaces;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Cash.UseCases;

public sealed record GetFlujoEfectivoRealQuery(DateTime Desde, DateTime Hasta)
    : IRequest<Result<IReadOnlyList<FlujoEfectivoDiaDto>>>;

public sealed class GetFlujoEfectivoRealQueryHandler
    : IRequestHandler<GetFlujoEfectivoRealQuery, Result<IReadOnlyList<FlujoEfectivoDiaDto>>>
{
    private readonly ICajaRepository _caja;
    private readonly IAccountingRepository _accounting;
    private readonly ICurrentTenant _tenant;

    public GetFlujoEfectivoRealQueryHandler(
        ICajaRepository caja,
        IAccountingRepository accounting,
        ICurrentTenant tenant)
    {
        _caja        = caja;
        _accounting = accounting;
        _tenant     = tenant;
    }

    public async Task<Result<IReadOnlyList<FlujoEfectivoDiaDto>>> Handle(
        GetFlujoEfectivoRealQuery request,
        CancellationToken ct)
    {
        var ids = new HashSet<Guid>();

        foreach (var c in await _caja.ListCuentasBancariasAsync(ct))
        {
            if (c.CuentaContableId is { } bid)
                ids.Add(bid);
        }

        foreach (var c in await _caja.ListCajasChicasAsync(ct))
        {
            if (c.CuentaContableCajaId is { } cid)
                ids.Add(cid);
        }

        if (ids.Count == 0)
        {
            return Result<IReadOnlyList<FlujoEfectivoDiaDto>>.Success(
                Array.Empty<FlujoEfectivoDiaDto>());
        }

        var lines = await _accounting.GetPostedLineAmountsByAccountsAsync(
            _tenant.TenantId,
            ids.ToList(),
            request.Desde,
            request.Hasta,
            ct);

        var porDia = lines
            .GroupBy(x => DateOnly.FromDateTime(x.EntryDate))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var entradas = g.Sum(x => x.Credit);
                var salidas  = g.Sum(x => x.Debit);
                var neto     = entradas - salidas;
                return new FlujoEfectivoDiaDto(g.Key, entradas, salidas, neto);
            })
            .ToList();

        return Result<IReadOnlyList<FlujoEfectivoDiaDto>>.Success(porDia);
    }
}
