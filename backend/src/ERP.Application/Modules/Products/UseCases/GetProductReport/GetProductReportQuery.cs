using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.UseCases.GetProductReport;

public sealed record GetProductReportQuery(
    ProductReportFilter Filter,
    int PageNumber,
    int PageSize
) : IRequest<Result<PagedResult<ProductReportItemDto>>>, ICompanyScopedRequest;
