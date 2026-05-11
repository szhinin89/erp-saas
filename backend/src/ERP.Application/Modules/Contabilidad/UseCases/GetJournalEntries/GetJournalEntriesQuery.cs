using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Contabilidad.DTOs;

namespace ERP.Application.Modules.Contabilidad.UseCases.GetJournalEntries;

public sealed record GetJournalEntriesQuery(
    int PageNumber,
    int PageSize
) : IRequest<Result<PagedResult<JournalEntryDto>>>;
