using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.GetProductFullReport;

public sealed record GetProductFullReportQuery(Guid Id) : IRequest<Result<ProductFullReportDto>>;
