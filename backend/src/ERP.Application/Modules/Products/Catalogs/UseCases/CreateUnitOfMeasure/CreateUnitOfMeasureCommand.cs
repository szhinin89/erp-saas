using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;

namespace ERP.Application.Products.Catalogs.UseCases.CreateUnitOfMeasure;

public record CreateUnitOfMeasureCommand(string Code, string Name, string? Symbol = null) : IRequest<Result<UnitOfMeasureDto>>;

