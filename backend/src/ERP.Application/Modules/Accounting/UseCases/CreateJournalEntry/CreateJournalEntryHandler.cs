// Poner esto:
using ERP.Application.Accounting.DTOs;
using ERP.Application.Accounting.UseCases.CreateJournalEntry;
using ERP.Application.Common;
using ERP.Domain.Accounting.Entities;
using ERP.Domain.Accounting.Interfaces;
using ERP.Domain.Accounting.ValueObjects;
namespace ERP.Application.Modules.Accounting.UseCases.CreateJournalEntry;

public class CreateJournalEntryHandler
{
    private readonly IAccountingRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWork _unitOfWork;

    public CreateJournalEntryHandler(
        IAccountingRepository repository,
        ICurrentTenant currentTenant,
        IUnitOfWork unitOfWork)
    {
        _repository    = repository;
        _currentTenant = currentTenant;
        _unitOfWork    = unitOfWork;
    }

    public async Task<Result<JournalEntryDto>> HandleAsync(
        CreateJournalEntryCommand command,
        CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;

        var entry = JournalEntry.Create(
            tenantId,
            command.Reference,
            command.Date,
            command.Description,
            tenantId);

        foreach (var line in command.Lines)
        {
            entry.AddLine(
                line.AccountId,
                new Money(line.DebitAmount, line.Currency),
                new Money(line.CreditAmount, line.Currency));
        }

        entry.Post(tenantId);

        await _repository.AddJournalEntryAsync(entry, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<JournalEntryDto>.Success(new JournalEntryDto(
            entry.Id,
            entry.Reference,
            entry.Date,
            entry.Description,
            entry.Status,           // ← era entry.IsPosted, ahora es entry.Status
            entry.Lines.Select(l => new JournalEntryLineDto(
                l.Id,
                l.AccountId,
                l.Debit.Amount,
                l.Debit.Currency,
                l.Credit.Amount,
                l.Credit.Currency)).ToList(),
            entry.CreatedAt));
    }
}