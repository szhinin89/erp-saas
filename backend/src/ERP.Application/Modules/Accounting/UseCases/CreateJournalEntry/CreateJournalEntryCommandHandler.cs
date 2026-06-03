using MediatR;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Application.Modules.Accounting.UseCases.CreateJournalEntry;
using ERP.Application.Common;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;

namespace ERP.Application.Modules.Accounting.UseCases.CreateJournalEntry;

public sealed class CreateJournalEntryCommandHandler : IRequestHandler<CreateJournalEntryCommand, Result<JournalEntryDto>>
{
    private readonly IAccountingRepository _repository;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public CreateJournalEntryCommandHandler(
        IAccountingRepository repository,
        ICurrentSubscriber currentSubscriber,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _repository        = repository;
        _currentSubscriber = currentSubscriber;
        _currentCompany    = currentCompany;
        _currentUser       = currentUser;
        _unitOfWork        = unitOfWork;
    }

    public async Task<Result<JournalEntryDto>> Handle(
        CreateJournalEntryCommand command,
        CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var companyId    = _currentCompany.CompanyId;
        var userId       = _currentUser.UserId;

        // Period-locking check: if a period exists for the entry date and it's closed, reject
        var period = await _repository.GetPeriodAsync(
            subscriberId, command.Date.Year, command.Date.Month, ct);
        if (period is { IsClosed: true })
            return Result<JournalEntryDto>.Failure(
                $"El período contable {period.Name} está cerrado. No se pueden registrar asientos en períodos cerrados.");

        var entry = JournalEntry.Create(
            subscriberId,
            companyId,
            command.Reference,
            command.Date,
            command.Description,
            userId);

        if (period is not null)
            entry.SetPeriod(period.Id);

        foreach (var accountId in command.Lines.Select(l => l.AccountId).Distinct())
        {
            var acc = await _repository.GetByIdAsync(accountId, subscriberId, ct);
            if (acc is null)
                return Result<JournalEntryDto>.Failure($"La cuenta {accountId} no existe o no pertenece al tenant.");
            if (!acc.IsActive)
                return Result<JournalEntryDto>.Failure($"La cuenta {acc.Code.Value} está deshabilitada.");
            if (!acc.AllowsMovements)
                return Result<JournalEntryDto>.Failure(
                    $"La cuenta {acc.Code.Value} es de agrupación y no admite movimientos en asientos.");
        }

        foreach (var line in command.Lines)
        {
            entry.AddLine(
                line.AccountId,
                new Money(line.DebitAmount,  line.Currency),
                new Money(line.CreditAmount, line.Currency));
        }

        // El asiento queda en Draft. Para contabilizarlo se usa un endpoint dedicado.

        var ownsTransaction = !_unitOfWork.HasActiveTransaction;
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _repository.AddJournalEntryAsync(entry, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            if (ownsTransaction)
                await _unitOfWork.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            if (ownsTransaction)
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
