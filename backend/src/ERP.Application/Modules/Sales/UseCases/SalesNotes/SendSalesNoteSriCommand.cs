using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Sales.UseCases.SalesNotes;

public sealed record SendSalesNoteSriCommand(Guid NoteId) : IRequest<Result<Guid>>, ICompanyScopedRequest;
