using ERP.Application.Modules.Caja.DTOs;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;

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
    /// <param name="userNameById">
    /// TREASURY-CASH-MANUAL-MOVEMENTS-01 — nombres de usuario ya resueltos en batch (una consulta
    /// para todos los <c>CreatedBy</c> distintos de los movimientos) por el llamador — nunca una
    /// consulta por movimiento. Vacío por defecto para no romper callers que no lo necesiten.
    /// </param>
    public static CashSessionDto ToDto(
        CashSession s,
        string? emissionType,
        (Guid? Id, string? Name) defaultWarehouse,
        (Guid? Id, string? Name) defaultCustomer,
        IReadOnlyDictionary<Guid, string>? userNameById = null
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
            s.Movements.Select(m => MapMovement(m, userNameById)).ToList(),
            s.ClosingCounts.Select(MapClosingCount).ToList(),
            s.CreatedAt,
            s.UpdatedAt
        );

    /// <summary>
    /// CASH-SESSION-LIST-SUMMARY-01 — fila del listado de turnos. <paramref name="collectionRows"/>
    /// es el subconjunto de filas (de un solo query por página, ver
    /// <c>ISalesInvoiceRepository.GetCollectionSummaryByCashSessionsAsync</c>) que pertenece a ESTA
    /// sesión — nunca una consulta propia por fila (evita N+1). <paramref name="methodById"/> es el
    /// catálogo de PaymentMethod ya cargado una vez para toda la página.
    /// </summary>
    public static CashSessionListDto ToListDto(
        CashSession s,
        string? userName,
        string? closedByName,
        IReadOnlyList<SalesInvoiceCashSessionPaymentRow> collectionRows,
        IReadOnlyDictionary<Guid, PaymentMethod> methodById
    )
    {
        var expectedCash = s.Status == CashSessionStatus.Closed
            ? s.ExpectedAmount ?? s.CurrentBalance
            : s.CurrentBalance;

        var invoiceCount = collectionRows.Select(r => r.InvoiceId).Distinct().Count();
        var totalInvoiced = collectionRows.GroupBy(r => r.InvoiceId).Sum(g => g.First().GrandTotal);

        var saleIncomeCash = s.Movements
            .Where(m => m.MovementType == CashMovementType.SaleIncome)
            .Sum(m => m.Amount);
        var manualIncomeCash = s.Movements
            .Where(m => m.MovementType == CashMovementType.ManualIncome)
            .Sum(m => m.Amount);
        // "Egresos manuales" = acciones deliberadas del cajero (ManualExpense/Withdrawal) — nunca
        // incluye SaleRefund, que es el reverso automático de una devolución, no una acción manual.
        var manualExpenseCash = s.Movements
            .Where(m => m.MovementType is CashMovementType.ManualExpense or CashMovementType.Withdrawal)
            .Sum(m => m.Amount);

        var byMethod = collectionRows
            .GroupBy(r => r.PaymentMethodId)
            .Select(g =>
            {
                methodById.TryGetValue(g.Key, out var method);
                return new CashSessionListCollectionByMethodDto(
                    g.Key,
                    g.First().PaymentMethodCode,
                    g.First().PaymentMethodName,
                    method?.IsCreditAllowed ?? false,
                    g.Select(r => r.InvoiceId).Distinct().Count(),
                    g.Sum(r => r.Amount)
                );
            })
            .OrderBy(m => PaymentMethodDisplayOrder.Rank(methodById.GetValueOrDefault(m.PaymentMethodId)))
            .ThenBy(m => m.PaymentMethodName)
            .ToList();

        return new CashSessionListDto(
            s.Id,
            s.UserId,
            userName,
            s.CashRegisterId,
            s.CashRegisterCodeSnapshot,
            s.CashRegisterNameSnapshot,
            s.EmissionPointId,
            s.EmissionPointCodeSnapshot,
            s.OpenedAt,
            s.OpeningAmount,
            s.Status.ToString(),
            s.CurrentBalance,
            expectedCash,
            s.CountedAmount,
            s.Movements.Count,
            s.ClosedAt,
            s.ClosedBy,
            closedByName,
            s.Difference,
            invoiceCount,
            totalInvoiced,
            saleIncomeCash,
            manualIncomeCash,
            manualExpenseCash,
            byMethod,
            s.CreatedAt
        );
    }

    private static CashMovementDto MapMovement(
        CashMovement m,
        IReadOnlyDictionary<Guid, string>? userNameById = null
    ) =>
        new(
            m.Id,
            m.MovementType.ToString(),
            m.Amount,
            m.Description,
            m.CreatedAt,
            m.CreatedBy,
            userNameById?.GetValueOrDefault(m.CreatedBy),
            m.ReferenceType.ToString(),
            m.ReferenceId,
            m.ReferenceNumber,
            m.ReasonId,
            m.ReasonName
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
            r.AccountingAccountId,
            r.CreatedAt,
            r.UpdatedAt
        );

    public static CashMovementReasonDto ToDto(CashMovementReason r) =>
        new(r.Id, r.Code, r.Name, r.MovementType.ToString(), r.IsActive, r.SortOrder);
}
