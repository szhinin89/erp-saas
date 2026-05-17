using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.UpdateProductSubcategory;

public record UpdateProductSubcategoryCommand(Guid Id, string Code, string Name, Guid CategoryId) : IRequest<Result<ProductSubcategoryDto>>;
