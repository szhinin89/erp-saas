using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.DisableProductSubcategory;

public record DisableProductSubcategoryCommand(Guid Id) : IRequest<Result<ProductSubcategoryDto>>;
