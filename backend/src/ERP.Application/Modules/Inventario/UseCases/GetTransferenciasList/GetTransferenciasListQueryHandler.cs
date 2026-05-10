using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventario.DTOs;
using ERP.Domain.Inventario.Entities;
using ERP.Domain.Inventario.Interfaces;

namespace ERP.Application.Inventario.UseCases.GetTransferenciasList;

public sealed class GetTransferenciasListQueryHandler
    : IRequestHandler<GetTransferenciasListQuery, Result<TransferenciasPagedResult>>
{
    private readonly ITransferenciaRepository _repo;
    private readonly ICurrentTenant           _currentTenant;

    public GetTransferenciasListQueryHandler(
        ITransferenciaRepository repo,
        ICurrentTenant currentTenant)
    {
        _repo          = repo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<TransferenciasPagedResult>> Handle(
        GetTransferenciasListQuery query, CancellationToken ct)
    {
        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize   = Math.Clamp(query.PageSize, 1, 100);

        var (items, total) = await _repo.GetPagedAsync(
            _currentTenant.TenantId,
            pageNumber, pageSize,
            query.BodegaOrigenId, query.BodegaDestinoId,
            query.Estado, query.FechaDesde, query.FechaHasta,
            ct);

        var dtos = items.Select(ToDto).ToList();
        return Result<TransferenciasPagedResult>.Success(
            new TransferenciasPagedResult(dtos, total, pageNumber, pageSize));
    }

    private static TransferenciaDto ToDto(Transferencia t) => new(
        t.Id, t.NumeroTransferencia,
        t.BodegaOrigenId,
        t.BodegaOrigen?.Nombre ?? t.BodegaOrigenId.ToString(),
        t.BodegaDestinoId,
        t.BodegaDestino?.Nombre ?? t.BodegaDestinoId.ToString(),
        t.FechaTransferencia, t.Estado,
        t.Motivo, t.Observaciones,
        t.FechaConfirmacion, t.ConfirmadoPor,
        t.CreatedAt);
}
