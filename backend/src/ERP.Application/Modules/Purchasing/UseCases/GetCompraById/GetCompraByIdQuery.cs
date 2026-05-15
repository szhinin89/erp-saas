using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;

namespace ERP.Application.Modules.Purchasing.UseCases.GetCompraById;

public sealed record GetPurchaseByIdQuery(Guid Id)
    : IRequest<Result<PurchBillDetailDto?>>;
