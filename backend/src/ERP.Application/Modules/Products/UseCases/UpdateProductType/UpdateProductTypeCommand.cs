using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using MediatR;

namespace ERP.Application.Products.UseCases.UpdateProductType;

public record UpdateProductTypeCommand(Guid ProductTypeId, string Code, string Name) : IRequest<Result<ProductTypeDto>>;
