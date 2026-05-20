using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.UseCases.VoidJournalEntry;

public sealed class VoidJournalEntryCommandHandler : IRequestHandler<VoidJournalEntryCommand, Result<JournalEntryDto>>
{
    private readonly IAccountingRepository _repository;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;

    public VoidJournalEntryCommandHandler(
        IAccountingRepository repository,
        IUserActivityRepository activity,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser)
    {
        _repository    = repository;
        _activity      = activity;
        _currentSubscriber = currentSubscriber;
        _currentUser   = currentUser;
    }

    public async Task<Result<JournalEntryDto>> Handle(VoidJournalEntryCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId   = _currentUser.UserId;

        var entry = await _repository.GetJournalEntryByIdAsync(command.Id, subscriberId, ct);
        if (entry is null)
            return Result<JournalEntryDto>.Failure("Asiento contable no encontrado.");

        try
        {
            entry.Void(userId, command.Reason);
        }
        catch (InvalidOperationException ex)
        {
            return Result<JournalEntryDto>.Failure(ex.Message);
        }

        await _activity.AddAsync(UserActivity.Create(
            subscriberId,
            userId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "accounting",
            action: "journalEntry.void",
            entityType: "JournalEntry",
            entityId: entry.Id,
            description: $"{entry.Reference} — {command.Reason}"), ct);

        await _repository.SaveChangesAsync(ct);

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
