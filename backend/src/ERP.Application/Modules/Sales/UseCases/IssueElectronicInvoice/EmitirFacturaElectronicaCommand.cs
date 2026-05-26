using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Sales.UseCases.IssueElectronicInvoice;

[RequireFeature(SubscriptionFeatureCodes.Sales)]
public sealed record IssueElectronicInvoiceCommand(Guid VentaId) : IRequest<Result<Guid>>, ICompanyScopedRequest;
