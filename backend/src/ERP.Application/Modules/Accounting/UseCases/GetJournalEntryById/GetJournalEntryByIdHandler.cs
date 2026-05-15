using ERP.Application.Common;
using ERP.Application.Modules.Accounting.DTOs;
using MediatR;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.UseCases.GetJournalEntryById;

public class GetJournalEntryByIdHandler : IRequestHandler<GetJournalEntryByIdQuery, Result<JournalEntryDto>>
{
    private readonly IAccountingRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public GetJournalEntryByIdHandler(IAccountingRepository repository, ICurrentTenant currentTenant)
    {
        _repository    = repository;
        _currentTenant = currentTenant;
    }

    public Task<Result<JournalEntryDto>> HandleAsync(Guid id, CancellationToken ct = default)
        => Handle(new GetJournalEntryByIdQuery(id), ct);

    public async Task<Result<JournalEntryDto>> Handle(GetJournalEntryByIdQuery request, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var entry    = await _repository.GetJournalEntryByIdAsync(request.Id, tenantId, ct);

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
