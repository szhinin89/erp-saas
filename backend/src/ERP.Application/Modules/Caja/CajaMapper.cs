using ERP.Application.Modules.Caja.DTOs;
using ERP.Domain.Modules.Caja.Entities;

namespace ERP.Application.Modules.Caja;

internal static class CajaMapper
{
    /// <param name="s"></param>
    /// <param name="emissionType">
    /// Debe provenir siempre de una consulta en vivo a <c>IEmissionPointRepository</c> por
    /// <see cref="CashSession.EmissionPointId"/> — nunca inferirse aquí ni quemarse un valor
    /// por defecto (evita duplicar la fuente de verdad de <c>EmissionPoint.EmissionType</c>).
    /// </param>
    /// <param name="defaultWarehouse">
    /// Bodega/cliente por defecto de la <c>CashRegister</c> dueña de esta sesión — resueltos en
    /// vivo por el llamador (vía <c>ICashRegisterRepository</c>, que ya incluye ambas navs), nunca
    /// un snapshot propio de <see cref="CashSession"/>.
    /// </param>
    /// <param name="defaultCustomer"></param>
    public static CashSessionDto ToDto(
        CashSession s,
        string? emissionType,
        (Guid? Id, string? Name) defaultWarehouse,
        (Guid? Id, string? Name) defaultCustomer
    ) =>
        new(
            s.Id,
            s.CompanyId,
            s.BranchId,
            s.UserId,
            s.CashRegisterId,
            s.CashRegisterCodeSnapshot,
            s.CashRegisterNameSnapshot,
            s.EmissionPointId,
            s.EmissionPointCodeSnapshot,
            emissionType,
            defaultWarehouse.Id,
            defaultWarehouse.Name,
            defaultCustomer.Id,
            defaultCustomer.Name,
            s.OpenedAt,
            s.OpeningAmount,
            s.Status.ToString(),
            s.Notes,
            s.ClosedAt,
            s.ClosedBy,
            s.CloseNotes,
            s.ExpectedAmount,
            s.CountedAmount,
            s.Difference,
            s.TotalIncome,
            s.TotalExpense,
            s.CurrentBalance,
            s.Movements.Select(MapMovement).ToList(),
            s.ClosingCounts.Select(MapClosingCount).ToList(),
            s.CreatedAt,
            s.UpdatedAt
        );

    public static CashSessionListDto ToListDto(CashSession s) =>
        new(
            s.Id,
            s.UserId,
            s.CashRegisterId,
            s.CashRegisterCodeSnapshot,
            s.EmissionPointId,
            s.OpenedAt,
            s.OpeningAmount,
            s.Status.ToString(),
            s.CurrentBalance,
            s.Movements.Count,
            s.ClosedAt,
            s.Difference,
            s.CreatedAt
        );

    private static CashMovementDto MapMovement(CashMovement m) =>
        new(
            m.Id,
            m.MovementType.ToString(),
            m.Amount,
            m.Description,
            m.CreatedAt,
            m.CreatedBy,
            m.ReferenceType.ToString(),
            m.ReferenceId,
            m.ReferenceNumber
        );

    private static CashClosingCountDto MapClosingCount(CashClosingCount c) =>
        new(c.Id, c.DenominationValue, c.DenominationLabel, c.Quantity, c.Total);

    /// <param name="r"></param>
    /// <param name="hasHistory">
    /// Debe provenir siempre de <see cref="ICashRegisterUsageGuard"/> — nunca inferirse aquí ni
    /// en el llamador a partir de otra señal (evita duplicar la regla de negocio).
    /// </param>
    public static CashRegisterDto ToDto(CashRegister r, bool hasHistory) =>
        new(
            r.Id,
            r.BranchId,
            r.Branch.Name,
            r.Branch.Code,
            r.EmissionPointId,
            r.EmissionPoint?.Establishment.Code,
            r.EmissionPoint?.Code,
            r.EmissionPoint?.Name,
            r.Code,
            r.Name,
            r.Notes,
            r.IsActive,
            hasHistory,
            r.DefaultWarehouseId,
            r.DefaultWarehouse?.Code,
            r.DefaultWarehouse?.Name,
            r.DefaultCustomerId,
            r.DefaultCustomer?.Name.LegalName,
            r.CreatedAt,
            r.UpdatedAt
        );
}
