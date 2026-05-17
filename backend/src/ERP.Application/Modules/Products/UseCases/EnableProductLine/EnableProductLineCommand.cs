using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.EnableProductLine;

public record EnableProductLineCommand(Guid Id) : IRequest<Result<ProductLineDto>>;
