using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;

namespace ERP.Application.Products.Catalogs.UseCases.DisableProductLine;

public record DisableProductLineCommand(Guid Id) : IRequest<Result<ProductLineDto>>;
