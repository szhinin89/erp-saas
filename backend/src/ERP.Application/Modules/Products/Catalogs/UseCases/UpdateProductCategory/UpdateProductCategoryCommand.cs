using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;

namespace ERP.Application.Products.Catalogs.UseCases.UpdateProductCategory;

public record UpdateProductCategoryCommand(Guid Id, string Code, string Name, Guid LineId) : IRequest<Result<ProductCategoryDto>>;
