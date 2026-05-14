using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;

namespace ERP.Application.Products.Catalogs.UseCases.CreateProductLine;

public record CreateProductLineCommand(string Code, string Name) : IRequest<Result<ProductLineDto>>;
