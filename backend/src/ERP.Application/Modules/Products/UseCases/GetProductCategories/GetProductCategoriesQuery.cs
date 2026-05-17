using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.GetProductCategories;

public record GetProductCategoriesQuery(Guid? LineId, bool? ActiveFilter, string? Search) : IRequest<Result<IReadOnlyList<ProductCategoryListItemDto>>>;
