using MediatR;
using ERP.Application.Accounting.DTOs;
using ERP.Application.Accounting.UseCases.CreateJournalEntry;
using ERP.Application.Common;
using ERP.Domain.Accounting.Entities;
using ERP.Domain.Accounting.Interfaces;
using ERP.Domain.Accounting.ValueObjects;

namespace ERP.Application.Modules.Accounting.UseCases.CreateJournalEntry;

public sealed class CreateJournalEntryCommandHandler : IRequestHandler<CreateJournalEntryCommand, Result<JournalEntryDto>>
{
    private readonly IAccountingRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public CreateJournalEntryCommandHandler(
        IAccountingRepository repository,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _repository    = repository;
        _currentTenant = currentTenant;
        _currentUser   = currentUser;
        _unitOfWork    = unitOfWork;
    }

    public async Task<Result<JournalEntryDto>> Handle(
        CreateJournalEntryCommand command,
        CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        var entry = JournalEntry.Create(
            tenantId,
            command.Reference,
            command.Date,
            command.Description,
            userId);

        foreach (var line in command.Lines)
        {
            entry.AddLine(
                line.AccountId,
                new Money(line.DebitAmount,  line.Currency),
                new Money(line.CreditAmount, line.Currency));
        }

        // El asiento queda en Draft. Para contabilizarlo se usa un endpoint dedicado.

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _repository.AddJournalEntryAsync(entry, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            return Result<JournalEntryDto>.Failure($"No se pudo crear el asiento: {ex.Message}");
        }

        return Result<JournalEntryDto>.Success(new JournalEntryDto(
            entry.Id,
            entry.Reference,
            entry.Date,
            entry.Description,
            entry.Status,
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
