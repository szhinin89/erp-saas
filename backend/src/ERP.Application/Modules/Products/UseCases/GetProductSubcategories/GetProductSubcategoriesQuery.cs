using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.GetProductSubcategories;

public record GetProductSubcategoriesQuery(Guid? LineId, Guid? CategoryId, bool? ActiveFilter, string? Search) : IRequest<Result<IReadOnlyList<ProductSubcategoryListItemDto>>>;
