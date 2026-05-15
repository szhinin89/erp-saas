using MediatR;
using ERP.Application.Common;
using ERP.Application.Sales.DTOs;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Sales.UseCases.Notas;

public record GetSalesNotesListQuery(Guid? OriginalBillId, string? Status)
    : IRequest<Result<IReadOnlyList<SalesNoteListItemDto>>>;


public sealed class GetSalesNotesListQueryHandler
    : IRequestHandler<GetSalesNotesListQuery, Result<IReadOnlyList<SalesNoteListItemDto>>>
{
    private readonly ISalesRepository _ventasRepository;
    private readonly ICurrentTenant  _currentTenant;

    public GetSalesNotesListQueryHandler(ISalesRepository ventasRepository, ICurrentTenant currentTenant)
    {
        _ventasRepository = ventasRepository;
        _currentTenant    = currentTenant;
    }

    public async Task<Result<IReadOnlyList<SalesNoteListItemDto>>> Handle(
        GetSalesNotesListQuery request,
        CancellationToken ct)
    {
        var items = await _ventasRepository.GetNotesAsync(
            _currentTenant.TenantId, request.OriginalBillId, request.Status, ct);

        var dto = items.Select(n => new SalesNoteListItemDto(
            n.Id,
            n.OriginalBillId,
            n.NoteType,
            n.Status,
            n.AccessKey,
            n.Total,
            n.IssueDate)).ToList();

        return Result<IReadOnlyList<SalesNoteListItemDto>>.Success(dto);
    }
}