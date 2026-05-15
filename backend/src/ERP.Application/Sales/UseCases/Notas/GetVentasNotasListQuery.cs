using MediatR;
using ERP.Application.Common;
using ERP.Application.Sales.DTOs;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Sales.UseCases.Notas;

public sealed record GetVentasNotasListQuery(Guid? OriginalBillId, string? Status)
    : IRequest<Result<IReadOnlyList<VentasNotaListItemDto>>>;

public sealed class GetVentasNotasListQueryHandler
    : IRequestHandler<GetVentasNotasListQuery, Result<IReadOnlyList<VentasNotaListItemDto>>>
{
    private readonly ISalesRepository _ventasRepository;
    private readonly ICurrentTenant  _currentTenant;

    public GetVentasNotasListQueryHandler(ISalesRepository ventasRepository, ICurrentTenant currentTenant)
    {
        _ventasRepository = ventasRepository;
        _currentTenant    = currentTenant;
    }

    public async Task<Result<IReadOnlyList<VentasNotaListItemDto>>> Handle(
        GetVentasNotasListQuery request,
        CancellationToken ct)
    {
        var items = await _ventasRepository.GetNotesAsync(
            _currentTenant.TenantId, request.OriginalBillId, request.Status, ct);

        var dto = items.Select(n => new VentasNotaListItemDto(
            n.Id,
            n.OriginalBillId,
            n.NoteType,
            n.Status,
            n.AccessKey,
            n.Total,
            n.IssueDate)).ToList();

        return Result<IReadOnlyList<VentasNotaListItemDto>>.Success(dto);
    }
}
