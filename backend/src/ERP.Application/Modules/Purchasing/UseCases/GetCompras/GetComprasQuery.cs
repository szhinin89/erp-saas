using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Domain.Modules.Purchasing.Enums;

namespace ERP.Application.Modules.Purchasing.UseCases.GetCompras;

public record GetPurchasesQuery(
    PurchaseStatus? Status,
    Guid?          BusinessPartnerId,
    DateTime?      DateFrom,
    DateTime?      DateTo,
    string?       Search
) : IRequest<Result<IReadOnlyList<PurchBillDto>>>, ICompanyScopedRequest;
