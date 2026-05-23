using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.DTOs;

namespace ERP.Application.Modules.Accounting.UseCases.GetJournalEntries;

public sealed record GetJournalEntriesQuery(
    int PageNumber,
    int PageSize
) : IRequest<Result<PagedResult<JournalEntryDto>>>, ICompanyScopedRequest;
