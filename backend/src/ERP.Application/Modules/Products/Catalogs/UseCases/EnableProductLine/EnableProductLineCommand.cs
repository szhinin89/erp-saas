using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;

namespace ERP.Application.Products.Catalogs.UseCases.EnableProductLine;

public record EnableProductLineCommand(Guid Id) : IRequest<Result<ProductLineDto>>;
