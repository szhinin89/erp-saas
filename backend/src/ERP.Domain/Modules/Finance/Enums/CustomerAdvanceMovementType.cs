namespace ERP.Domain.Modules.Finance.Enums;

/// <summary>
/// ADR-052 §4 — tipos del único libro de movimientos de un anticipo de cliente. Los valores se
/// persisten y los referencian restricciones físicas: no renumerar.
/// </summary>
public enum CustomerAdvanceMovementType
{
    /// <summary>Ingreso original del anticipo.</summary>
    Received = 1,

    /// <summary>Aplicación a una cuenta por cobrar (una línea por movimiento).</summary>
    Application = 2,

    ReversalOfApplication = 3,

    /// <summary>Devolución efectiva de dinero al cliente.</summary>
    Refund = 4,

    ReversalOfRefund = 5,

    /// <summary>Anulación administrativa: el dinero nunca fue recibido.</summary>
    SourceCancelled = 6,

    /// <summary>Reversa de un ingreso que realmente se deshizo.</summary>
    IncomeReversed = 7,
}
