using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Contabilidad.DTOs;

namespace ERP.Application.Modules.Contabilidad.UseCases.VoidJournalEntry;

[RequireFeature(SubscriptionFeatureCodes.Accounting)]
public sealed record VoidJournalEntryCommand(
    Guid Id,
    string Reason
) : IRequest<Result<JournalEntryDto>>;
