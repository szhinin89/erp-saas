using ERP.Domain.Common;
using ERP.Domain.Modules.Caja.Enums;

namespace ERP.Domain.Modules.Caja.Entities;

/// <summary>
/// TREASURY-CASH-MANUAL-MOVEMENTS-01 — catálogo administrable de motivos para movimientos
/// manuales de caja (SSOT dinámico — CLAUDE.md § Catálogos y datos configurables). A diferencia de
/// <c>InventoryAdjustmentReason</c> (CompanyId nullable = tenant-wide), aquí el scope es SIEMPRE
/// Tenant+Company (obligatorio) — un motivo nunca se comparte entre empresas del mismo tenant.
/// <see cref="MovementType"/> restringe el motivo a exactamente un tipo manual
/// (<see cref="CashMovementType.ManualIncome"/>/<see cref="CashMovementType.ManualExpense"/>/
/// <see cref="CashMovementType.Withdrawal"/>) — nunca a los tipos de sistema (Opening/SaleIncome/
/// SaleRefund), que no admiten motivo elegido por el usuario.
/// </summary>
public sealed class CashMovementReason : MasterEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public const int CodeMaxLen = 30;
    public const int NameMaxLen = 100;

    public Guid CompanyId { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public CashMovementType MovementType { get; private set; }
    public int SortOrder { get; private set; }

    private CashMovementReason() { }

    public static CashMovementReason Create(
        Guid tenantId,
        Guid companyId,
        string code,
        string name,
        CashMovementType movementType,
        int sortOrder,
        Guid createdBy
    )
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("La empresa es obligatoria.", nameof(companyId));
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("El código es obligatorio.", nameof(code));
        if (code.Trim().Length > CodeMaxLen)
            throw new ArgumentException(
                $"El código no puede superar {CodeMaxLen} caracteres.",
                nameof(code)
            );
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre es obligatorio.", nameof(name));
        if (name.Trim().Length > NameMaxLen)
            throw new ArgumentException(
                $"El nombre no puede superar {NameMaxLen} caracteres.",
                nameof(name)
            );
        EnsureManualMovementType(movementType);

        var reason = new CashMovementReason
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            MovementType = movementType,
            SortOrder = sortOrder,
        };
        reason.SetCreated(createdBy);
        return reason;
    }

    /// <summary>
    /// TREASURY-CASH-MOVEMENT-REASONS-ADMIN-03A — Code y MovementType son inmutables tras la
    /// creación (mismo convenio que Code en otros catálogos: PaymentMethod, InventoryAdjustment
    /// Reason). Cambiar el tipo de un motivo ya usado reclasificaría movimientos históricos que
    /// referencian este Id (ReasonId/ReasonName en CashMovement) sin que el usuario lo pidiera —
    /// solo Name/SortOrder son editables; IsActive se gestiona exclusivamente vía Enable/Disable.
    /// </summary>
    public void Update(string name, int sortOrder, Guid updatedBy)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre es obligatorio.", nameof(name));
        if (name.Trim().Length > NameMaxLen)
            throw new ArgumentException(
                $"El nombre no puede superar {NameMaxLen} caracteres.",
                nameof(name)
            );

        Name = name.Trim();
        SortOrder = sortOrder;
        SetUpdated(updatedBy);
    }

    private static void EnsureManualMovementType(CashMovementType movementType)
    {
        if (
            movementType != CashMovementType.ManualIncome
            && movementType != CashMovementType.ManualExpense
            && movementType != CashMovementType.Withdrawal
        )
            throw new ArgumentException(
                "MovementType debe ser ManualIncome, ManualExpense o Withdrawal — los motivos no aplican a movimientos de sistema (Opening/SaleIncome/SaleRefund).",
                nameof(movementType)
            );
    }
}
