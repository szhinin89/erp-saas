using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.GetEmissionPoints;

/// <summary>
/// Devuelve todos los puntos de emisión de la empresa activa, con información del
/// establecimiento para mostrar en la pantalla independiente /settings/emission-points.
/// </summary>
public record GetAllEmissionPointsQuery(bool? ActiveFilter = null, string? Search = null)
    : IRequest<Result<IReadOnlyList<EmissionPointListItemDto>>>,
        ICompanyScopedRequest;
