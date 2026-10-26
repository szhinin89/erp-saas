using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using MediatR;

namespace ERP.Application.Modules.Inventory.AdjustmentReasons.UseCases.ListInventoryAdjustmentReasons;

/// <summary>
/// Motivos de la Company activa. <see cref="CompanyId"/> (query) es solo un hint opcional: si viene,
/// CompanyScopeBehavior exige que coincida con el contexto autenticado.
/// </summary>
public sealed record ListInventoryAdjustmentReasonsQuery(
    Guid? CompanyId,
    bool IncludeInactive = false
) : IRequest<Result<IReadOnlyList<InventoryAdjustmentReasonDto>>>, ICompanyScopedRequest
{
    Guid? ICompanyScopedRequest.ExplicitCompanyId => CompanyId;
}
