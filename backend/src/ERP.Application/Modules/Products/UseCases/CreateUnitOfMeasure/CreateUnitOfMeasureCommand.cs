using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.CreateUnitOfMeasure;

public record CreateUnitOfMeasureCommand(string Code, string Name, string? Symbol = null) : IRequest<Result<UnitOfMeasureDto>>;

