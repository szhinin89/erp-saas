namespace ERP.Domain.Modules.Finance.Enums;

/// <summary>
/// ADR-052 §4 — ciclo de vida de <see cref="Entities.CustomerAdvance"/>. Sin borrador: un anticipo
/// nace confirmado. Un anticipo agotado sigue <see cref="Confirmed"/>.
/// </summary>
public enum CustomerAdvanceStatus
{
    Confirmed = 1,

    /// <summary>Terminal: solo por anulación administrativa o reversa del ingreso.</summary>
    Cancelled = 2,
}
