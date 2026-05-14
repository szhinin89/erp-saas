using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;

namespace ERP.Application.Modules.Branches.UseCases.GetGeoParishes;

public record GetGeoParishesQuery(string CantonId) : IRequest<Result<IReadOnlyList<GeographyItemDto>>>;
