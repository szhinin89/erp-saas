using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.GetProductLines;

public record GetProductLinesQuery(bool? ActiveFilter, string? Search) : IRequest<Result<IReadOnlyList<ProductLineDto>>>;
