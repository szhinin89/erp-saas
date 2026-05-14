using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;

namespace ERP.Application.Products.Catalogs.UseCases.UpdateProductLine;

public record UpdateProductLineCommand(Guid Id, string Code, string Name) : IRequest<Result<ProductLineDto>>;
