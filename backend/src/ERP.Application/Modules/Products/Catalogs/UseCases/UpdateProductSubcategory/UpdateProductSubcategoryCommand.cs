using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;

namespace ERP.Application.Products.Catalogs.UseCases.UpdateProductSubcategory;

public record UpdateProductSubcategoryCommand(Guid Id, string Code, string Name, Guid CategoryId) : IRequest<Result<ProductSubcategoryDto>>;
