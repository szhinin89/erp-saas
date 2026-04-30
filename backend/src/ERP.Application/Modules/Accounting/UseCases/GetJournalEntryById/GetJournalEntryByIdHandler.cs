using ERP.Application.Common;
using ERP.Application.Accounting.DTOs;
using ERP.Domain.Accounting.Interfaces;

namespace ERP.Application.Accounting.UseCases.GetJournalEntryById;

public class GetJournalEntryByIdHandler
{
    private readonly IAccountingRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public GetJournalEntryByIdHandler(IAccountingRepository repository, ICurrentTenant currentTenant)
    {
        _repository    = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<JournalEntryDto>> HandleAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;
        var entry    = await _repository.GetJournalEntryByIdAsync(id, tenantId, ct);

        if (entry is null)
            return Result<JournalEntryDto>.Failure("Asiento contable no encontrado.");

        var dto = new JournalEntryDto(
            entry.Id, entry.Reference, entry.Date, entry.Description, entry.Status,
            entry.Lines.Select(l => new JournalEntryLineDto(
                l.Id, l.AccountId,
                l.Debit.Amount, l.Debit.Currency,
                l.Credit.Amount, l.Credit.Currency)).ToList(),
            entry.CreatedAt);

        return Result<JournalEntryDto>.Success(dto);
    }
}
