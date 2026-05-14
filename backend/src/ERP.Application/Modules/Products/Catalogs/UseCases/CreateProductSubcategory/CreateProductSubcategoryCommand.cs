using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;

namespace ERP.Application.Products.Catalogs.UseCases.CreateProductSubcategory;

public record CreateProductSubcategoryCommand(string Code, string Name, Guid CategoryId) : IRequest<Result<ProductSubcategoryDto>>;
