using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Contabilidad.DTOs;

namespace ERP.Application.Modules.Contabilidad.UseCases.GetJournalEntryById;

public sealed record GetJournalEntryByIdQuery(Guid Id) : IRequest<Result<JournalEntryDto>>;
