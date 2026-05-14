using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;

namespace ERP.Application.Products.Catalogs.UseCases.EnableProductSubcategory;

public record EnableProductSubcategoryCommand(Guid Id) : IRequest<Result<ProductSubcategoryDto>>;
