using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;

namespace ERP.Application.Products.Catalogs.UseCases.GetProductLines;

public record GetProductLinesQuery(bool? ActiveFilter, string? Search) : IRequest<Result<IReadOnlyList<ProductLineDto>>>;
