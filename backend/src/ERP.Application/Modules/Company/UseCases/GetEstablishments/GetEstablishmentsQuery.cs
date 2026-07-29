using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.GetEstablishments;

/// <summary>Devuelve todos los establecimientos de la empresa activa con filtros opcionales — para la pantalla /settings/establishments.</summary>
public record GetEstablishmentsQuery(Guid? BranchId, bool? IsActive, string? Search)
    : IRequest<Result<IReadOnlyList<EstablishmentListItemDto>>>,
        ICompanyScopedRequest;
