using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.DTOs;

namespace ERP.Application.Modules.Accounting.UseCases.CreateJournalEntry;

/// <summary>Nuevo asiento; cuenta contra el cupo de documentos del periodo si está medido.</summary>
[RequireFeature(SubscriptionFeatureCodes.Accounting)]
[ConsumeSubscriptionUnits(SubscriptionFeatureCodes.Documents, 1)]
public sealed record CreateJournalEntryCommand(
    string Reference,
    DateTime Date,
    string Description,
    IReadOnlyList<JournalEntryLineCommand> Lines
) : IRequest<Result<JournalEntryDto>>, ICompanyScopedRequest;

public record JournalEntryLineCommand(
    Guid AccountId,
    decimal DebitAmount,
    decimal CreditAmount,
    string Currency
);
