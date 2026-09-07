using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using MediatR;

namespace ERP.Application.MasterData.UseCases.AddSupplierRetentionDefault;

/// <summary>
/// RETENTIONS-SUPPLIER-DEFAULTS-DYNAMIC-01 — agrega una retención predeterminada al proveedor en
/// la empresa activa. Company-scoped: requiere company_id en el JWT (nunca del body). El código
/// debe existir y estar activo en el catálogo SRI (sri_retention_code) — validado en
/// <see cref="AddSupplierRetentionDefaultValidator"/>.
/// </summary>
public sealed record AddSupplierRetentionDefaultCommand(
    Guid BusinessPartnerId,
    Guid SriRetentionCodeId
) : IRequest<Result<SupplierRetentionDefaultDto>>, ICompanyScopedRequest;
