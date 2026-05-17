using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using MediatR;

namespace ERP.Application.Products.UseCases.DisableProductType;

public record DisableProductTypeCommand(Guid ProductTypeId) : IRequest<Result<ProductTypeDto>>;
public record EnableProductTypeCommand(Guid ProductTypeId) : IRequest<Result<ProductTypeDto>>;
