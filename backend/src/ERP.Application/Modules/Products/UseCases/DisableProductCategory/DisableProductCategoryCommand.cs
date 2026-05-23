using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.DisableProductCategory;

public record DisableProductCategoryCommand(Guid Id) : IRequest<Result<ProductCategoryDto>>, ICompanyScopedRequest;
