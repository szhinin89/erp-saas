using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;

namespace ERP.Application.Products.Catalogs.UseCases.DisableProductCategory;

public record DisableProductCategoryCommand(Guid Id) : IRequest<Result<ProductCategoryDto>>;
