using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Sales.UseCases.SalesNotes;

public sealed record SendSalesNoteSriCommand(Guid NotaId) : IRequest<Result<Guid>>, ICompanyScopedRequest;
