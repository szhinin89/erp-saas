using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;

namespace ERP.Application.Products.Catalogs.UseCases.GetProductCategories;

public record GetProductCategoriesQuery(Guid? LineId, bool? ActiveFilter, string? Search) : IRequest<Result<IReadOnlyList<ProductCategoryListItemDto>>>;
