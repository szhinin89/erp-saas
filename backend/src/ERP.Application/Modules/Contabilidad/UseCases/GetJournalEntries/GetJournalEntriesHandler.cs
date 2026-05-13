using ERP.Application.Common;
using ERP.Application.Modules.Contabilidad.DTOs;
using MediatR;
using ERP.Domain.Modules.Contabilidad.Interfaces;
using ERP.Domain.Common;

namespace ERP.Application.Modules.Contabilidad.UseCases.GetJournalEntries;

public class GetJournalEntriesHandler : IRequestHandler<GetJournalEntriesQuery, Result<PagedResult<JournalEntryDto>>>
{
    private readonly IAccountingRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public GetJournalEntriesHandler(IAccountingRepository repository, ICurrentTenant currentTenant)
    {
        _repository    = repository;
        _currentTenant = currentTenant;
    }

    public Task<Result<PagedResult<JournalEntryDto>>> HandleAsync(int pageNumber, int pageSize, CancellationToken ct = default)
        => Handle(new GetJournalEntriesQuery(pageNumber, pageSize), ct);

    public async Task<Result<PagedResult<JournalEntryDto>>> Handle(GetJournalEntriesQuery request, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var (entries, totalCount)  = await _repository.GetJournalEntriesPageAsync(
            tenantId,
            request.PageNumber,
            request.PageSize,
            ct);

        var dtos = entries.Select(e => new JournalEntryDto(
            e.Id, e.Reference, e.Date, e.Description, e.Status,
            e.Lines.Select(l => new JournalEntryLineDto(
                l.Id, l.AccountId,
                l.Debit.Amount, l.Debit.Currency,
                l.Credit.Amount, l.Credit.Currency)).ToList(),
            e.CreatedAt))
            .ToList();

        return Result<PagedResult<JournalEntryDto>>.Success(new PagedResult<JournalEntryDto>(
            Items: dtos,
            PageNumber: request.PageNumber,
            PageSize: request.PageSize,
            TotalCount: totalCount));
    }
}
