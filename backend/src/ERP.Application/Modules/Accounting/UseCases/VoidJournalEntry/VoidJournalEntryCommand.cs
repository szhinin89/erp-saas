using MediatR;
using ERP.Application.Common;
using ERP.Application.Accounting.DTOs;

namespace ERP.Application.Accounting.UseCases.VoidJournalEntry;

[RequireFeature(SubscriptionFeatureCodes.Accounting)]
public sealed record VoidJournalEntryCommand(
    Guid Id,
    string Reason
) : IRequest<Result<JournalEntryDto>>;
