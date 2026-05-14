using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;

namespace ERP.Application.Modules.Branches.UseCases.GetGeoCantons;

public record GetGeoCantonsQuery(string ProvinceId) : IRequest<Result<IReadOnlyList<GeographyItemDto>>>;
