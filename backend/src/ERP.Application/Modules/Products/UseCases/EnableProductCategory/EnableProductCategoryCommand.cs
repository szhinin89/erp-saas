using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.EnableProductCategory;

public record EnableProductCategoryCommand(Guid Id) : IRequest<Result<ProductCategoryDto>>;
