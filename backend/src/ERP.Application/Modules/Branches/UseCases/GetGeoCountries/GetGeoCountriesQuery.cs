using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;
using MediatR;

namespace ERP.Application.Modules.Branches.UseCases.GetGeoCountries;

public record GetGeoCountriesQuery : IRequest<Result<IReadOnlyList<GeographyItemDto>>>, ICompanyScopedRequest;
