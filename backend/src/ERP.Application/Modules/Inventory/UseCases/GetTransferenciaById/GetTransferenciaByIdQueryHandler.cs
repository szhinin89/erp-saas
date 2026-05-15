using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventory.DTOs;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Inventory.UseCases.GetTransferenciaById;

public sealed class GetTransferenciaByIdQueryHandler
    : IRequestHandler<GetTransferenciaByIdQuery, Result<TransferenciaDetailDto?>>
{
    private readonly ITransferenciaRepository _repo;
    private readonly ICurrentTenant           _currentTenant;

    public GetTransferenciaByIdQueryHandler(
        ITransferenciaRepository repo,
        ICurrentTenant currentTenant)
    {
        _repo          = repo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<TransferenciaDetailDto?>> Handle(
        GetTransferenciaByIdQuery query, CancellationToken ct)
    {
        var t = await _repo.GetByIdAsync(_currentTenant.TenantId, query.Id, ct);
        if (t is null)
            return Result<TransferenciaDetailDto?>.Success(null);

        var detalles = t.Detalles.Select(d => new TransferenciaDetalleDto(
            d.Id, d.ProductoId, d.Descripcion, d.Cantidad)).ToList();

        return Result<TransferenciaDetailDto?>.Success(new TransferenciaDetailDto(
            t.Id, t.NumeroTransferencia,
            t.BodegaOrigenId,
            t.BodegaOrigen?.Nombre ?? t.BodegaOrigenId.ToString(),
            t.BodegaDestinoId,
            t.BodegaDestino?.Nombre ?? t.BodegaDestinoId.ToString(),
            t.FechaTransferencia, t.Estado,
            t.Motivo, t.Observaciones,
            t.FechaConfirmacion, t.ConfirmadoPor,
            t.CreatedAt, detalles));
    }
}
