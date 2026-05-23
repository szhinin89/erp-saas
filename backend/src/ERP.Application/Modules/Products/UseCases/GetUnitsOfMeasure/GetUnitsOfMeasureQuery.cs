using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.DTOs;

namespace ERP.Application.Products.UseCases.GetUnitsOfMeasure;

public sealed record GetUnitsOfMeasureQuery(bool OnlyActive) : IRequest<Result<IReadOnlyList<UnitOfMeasureDto>>>, ICompanyScopedRequest;
