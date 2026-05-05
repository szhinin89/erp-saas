using MediatR;
using ERP.Application.Common;
using ERP.Application.Accounting.DTOs;

namespace ERP.Application.Accounting.UseCases.GetJournalEntryById;

public sealed record GetJournalEntryByIdQuery(Guid Id) : IRequest<Result<JournalEntryDto>>;
