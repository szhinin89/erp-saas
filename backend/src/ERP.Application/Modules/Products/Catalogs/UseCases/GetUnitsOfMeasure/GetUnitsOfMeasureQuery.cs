using MediatR;
using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;

namespace ERP.Application.Products.Catalogs.UseCases.GetUnitsOfMeasure;

public sealed record GetUnitsOfMeasureQuery(bool OnlyActive) : IRequest<Result<IReadOnlyList<UnitOfMeasureDto>>>;
