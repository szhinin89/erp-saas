using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.CreateProductCategory;

public record CreateProductCategoryCommand(string Code, string Name, Guid LineId) : IRequest<Result<ProductCategoryDto>>;
