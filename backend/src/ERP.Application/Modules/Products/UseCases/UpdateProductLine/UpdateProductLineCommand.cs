using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.UpdateProductLine;

public record UpdateProductLineCommand(Guid Id, string Code, string Name) : IRequest<Result<ProductLineDto>>;
