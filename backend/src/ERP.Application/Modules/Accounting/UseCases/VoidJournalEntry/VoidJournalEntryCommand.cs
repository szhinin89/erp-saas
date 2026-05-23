using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.DTOs;

namespace ERP.Application.Modules.Accounting.UseCases.VoidJournalEntry;

[RequireFeature(SubscriptionFeatureCodes.Accounting)]
public sealed record VoidJournalEntryCommand(
    Guid Id,
    string Reason
) : IRequest<Result<JournalEntryDto>>, ICompanyScopedRequest;
