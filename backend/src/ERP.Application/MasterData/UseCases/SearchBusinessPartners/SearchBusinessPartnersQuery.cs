using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Domain.MasterData.Enums;
using MediatR;

namespace ERP.Application.MasterData.UseCases.SearchBusinessPartners;

/// <summary>
/// Búsqueda paginada de BusinessPartners.
/// Roles: filtro extensible — reemplaza los obsoletos IsCustomer/IsSupplier booleans.
/// Ejemplo: Roles = [Customer, Supplier] devuelve BPs que tienen ALGUNO de esos roles activos.
/// </summary>
public sealed record SearchBusinessPartnersQuery(
    string?      Query    = null,
    bool?        IsActive = true,
    RoleType[]?  Roles    = null,
    int          Skip     = 0,
    int          Take     = 50)
    : IRequest<Result<PagedResult<BusinessPartnerSummaryDto>>>, ITenantScopedRequest;
