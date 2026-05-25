using ERP.Application.Common;
using ERP.Application.Modules.Commercial.DTOs;
using MediatR;

namespace ERP.Application.Modules.Commercial.UseCases.GetSalesOrderByPublicId;

public sealed record GetSalesOrderByPublicIdQuery(Guid PublicId)
    : IRequest<Result<SalesOrderDetailDto?>>, ICompanyScopedRequest;
