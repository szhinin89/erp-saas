using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.UpdateProductCategory;

public record UpdateProductCategoryCommand(Guid Id, string Code, string Name, Guid LineId) : IRequest<Result<ProductCategoryDto>>;
