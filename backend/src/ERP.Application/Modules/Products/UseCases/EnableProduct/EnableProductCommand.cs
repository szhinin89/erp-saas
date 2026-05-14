using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.EnableProduct;

public record EnableProductCommand(Guid Id) : IRequest<Result<ProductDto>>;
