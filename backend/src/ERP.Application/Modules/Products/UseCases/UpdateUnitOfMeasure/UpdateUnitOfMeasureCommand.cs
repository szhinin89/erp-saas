using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using MediatR;

namespace ERP.Application.Products.UseCases.UpdateUnitOfMeasure;

public record UpdateUnitOfMeasureCommand(
    Guid UnitOfMeasureId,
    string Code,
    string Name,
    string? Symbol = null) : IRequest<Result<UnitOfMeasureDto>>, ICompanyScopedRequest;
