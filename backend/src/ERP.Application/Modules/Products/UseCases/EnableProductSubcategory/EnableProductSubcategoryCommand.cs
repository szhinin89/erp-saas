using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.EnableProductSubcategory;

public record EnableProductSubcategoryCommand(Guid Id) : IRequest<Result<ProductSubcategoryDto>>;
