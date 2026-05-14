using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;

namespace ERP.Application.Products.Catalogs.UseCases.EnableProductCategory;

public record EnableProductCategoryCommand(Guid Id) : IRequest<Result<ProductCategoryDto>>;
