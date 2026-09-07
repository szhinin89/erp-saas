using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using MediatR;

namespace ERP.Application.MasterData.UseCases.GetSupplierRetentionDefaults;

/// <summary>
/// RETENTIONS-SUPPLIER-DEFAULTS-DYNAMIC-01 — lista completa (activas e inactivas) de retenciones
/// predeterminadas de un proveedor en la empresa activa. Company-scoped: requiere company_id en
/// el JWT (nunca del body).
/// </summary>
public sealed record GetSupplierRetentionDefaultsQuery(Guid BusinessPartnerId)
    : IRequest<Result<IReadOnlyList<SupplierRetentionDefaultDto>>>,
        ICompanyScopedRequest;
