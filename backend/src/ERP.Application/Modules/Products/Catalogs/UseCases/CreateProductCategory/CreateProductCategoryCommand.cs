using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;

namespace ERP.Application.Products.Catalogs.UseCases.CreateProductCategory;

public record CreateProductCategoryCommand(string Code, string Name, Guid LineId) : IRequest<Result<ProductCategoryDto>>;
