using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases.GetSalesReceiptPrintPayload;

public sealed record GetSalesReceiptPrintPayloadQuery(Guid InvoiceId)
    : IRequest<Result<SalesReceiptPrintPayloadDto>>,
        IBranchScopedRequest;
