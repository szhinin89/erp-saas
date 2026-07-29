using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;
using MediatR;

namespace ERP.Application.Modules.Branches.UseCases.GetGeoProvinces;

public record GetGeoProvincesQuery(string CountryId)
    : IRequest<Result<IReadOnlyList<GeographyItemDto>>>,
        ICompanyScopedRequest;
