using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;
using MediatR;

namespace ERP.Application.Modules.Branches.UseCases.GetGeoCantons;

public record GetGeoCantonsQuery(string ProvinceId)
    : IRequest<Result<IReadOnlyList<GeographyItemDto>>>,
        ICompanyScopedRequest;
