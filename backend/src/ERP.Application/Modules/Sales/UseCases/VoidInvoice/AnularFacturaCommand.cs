using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Sales.UseCases.VoidInvoice;

[RequireFeature(SubscriptionFeatureCodes.Sales)]
public sealed record VoidInvoiceCommand(Guid VentaId) : IRequest<Result<Guid>>, ICompanyScopedRequest;
