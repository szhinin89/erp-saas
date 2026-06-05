using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Sales.UseCases.IssueElectronicInvoice;

[RequireFeature(SubscriptionFeatureCodes.Sales)]
public sealed record IssueElectronicInvoiceCommand(Guid SaleId) : IRequest<Result<Guid>>, ICompanyScopedRequest;
