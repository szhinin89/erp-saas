using ERP.Application.Common;
using ERP.Application.Accounting.DTOs;
using ERP.Domain.Accounting.Interfaces;
using ERP.Domain.Common;

namespace ERP.Application.Accounting.UseCases.GetJournalEntries;

public class GetJournalEntriesHandler
{
    private readonly IAccountingRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public GetJournalEntriesHandler(IAccountingRepository repository, ICurrentTenant currentTenant)
    {
        _repository    = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<IReadOnlyList<JournalEntryDto>>> HandleAsync(CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;
        var entries  = await _repository.GetAllJournalEntriesAsync(tenantId, ct);

        var dtos = entries.Select(e => new JournalEntryDto(
            e.Id, e.Reference, e.Date, e.Description, e.Status,
            e.Lines.Select(l => new JournalEntryLineDto(
                l.Id, l.AccountId,
                l.Debit.Amount, l.Debit.Currency,
                l.Credit.Amount, l.Credit.Currency)).ToList(),
            e.CreatedAt))
            .ToList();

        return Result<IReadOnlyList<JournalEntryDto>>.Success(dtos);
    }
}
