using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;

namespace ERP.Application.Products.Catalogs.UseCases.GetProductSubcategories;

public record GetProductSubcategoriesQuery(Guid? LineId, Guid? CategoryId, bool? ActiveFilter, string? Search) : IRequest<Result<IReadOnlyList<ProductSubcategoryListItemDto>>>;
