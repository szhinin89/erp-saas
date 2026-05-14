using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.DisableProduct;

public record DisableProductCommand(Guid Id) : IRequest<Result<ProductDto>>;
