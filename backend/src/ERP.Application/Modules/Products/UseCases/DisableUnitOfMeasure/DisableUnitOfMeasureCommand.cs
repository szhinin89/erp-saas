using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using MediatR;

namespace ERP.Application.Products.UseCases.DisableUnitOfMeasure;

public record DisableUnitOfMeasureCommand(Guid UnitOfMeasureId) : IRequest<Result<UnitOfMeasureDto>>;
public record EnableUnitOfMeasureCommand(Guid UnitOfMeasureId)  : IRequest<Result<UnitOfMeasureDto>>;
