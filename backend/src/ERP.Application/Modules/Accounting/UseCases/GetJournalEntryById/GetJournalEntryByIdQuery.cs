using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.DTOs;

namespace ERP.Application.Modules.Accounting.UseCases.GetJournalEntryById;

public sealed record GetJournalEntryByIdQuery(Guid Id) : IRequest<Result<JournalEntryDto>>, ICompanyScopedRequest;
