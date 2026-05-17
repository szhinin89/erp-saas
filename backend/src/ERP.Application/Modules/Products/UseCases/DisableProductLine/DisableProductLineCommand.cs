using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.DisableProductLine;

public record DisableProductLineCommand(Guid Id) : IRequest<Result<ProductLineDto>>;
