using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;

namespace ERP.Application.Products.Catalogs.UseCases.DisableProductSubcategory;

public record DisableProductSubcategoryCommand(Guid Id) : IRequest<Result<ProductSubcategoryDto>>;
