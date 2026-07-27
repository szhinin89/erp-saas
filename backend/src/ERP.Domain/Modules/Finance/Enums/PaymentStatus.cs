namespace ERP.Domain.Modules.Finance.Enums;

/// <summary>
/// Estado tipado del agregado <c>Payment</c> (Fase 5.5.5.2) — mismo patrón de ciclo de vida que
/// <c>JournalEntryStatus</c> (ADR-026 Fase 5.2/5.4): Draft mientras se agregan líneas de
/// aplicación, Applied una vez validado el balance, Reversed como única vía para invalidarlo.
/// </summary>
public enum PaymentStatus
{
    Draft,
    Applied,
    Reversed,
}
