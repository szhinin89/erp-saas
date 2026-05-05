using MediatR;
using ERP.Application.Common;
using ERP.Application.Accounting.DTOs;

namespace ERP.Application.Accounting.UseCases.GetJournalEntries;

public sealed record GetJournalEntriesQuery(
    int PageNumber,
    int PageSize
) : IRequest<Result<PagedResult<JournalEntryDto>>>;
