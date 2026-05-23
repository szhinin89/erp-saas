using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;

namespace ERP.Application.Modules.Branches.UseCases.GetGeoProvinces;

public record GetGeoProvincesQuery(string CountryId) : IRequest<Result<IReadOnlyList<GeographyItemDto>>>, ICompanyScopedRequest;
