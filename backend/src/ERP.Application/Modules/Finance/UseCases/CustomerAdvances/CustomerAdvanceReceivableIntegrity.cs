using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Application.Modules.Finance.UseCases.CustomerAdvances;

/// <summary>
/// ADR-052 TA-06 — restricción exclusiva de Anticipos (no se aplica a los cobros): una CxC solo
/// admite una aplicación o su reversa si lo cobrado en sus cuotas coincide con
/// <c>PaidAmount</c>. No se corrigen saldos ni cuotas.
/// </summary>
internal static class CustomerAdvanceReceivableIntegrity
{
    public static void EnsureReversible(SalesReceivable receivable)
    {
        if (receivable.Installments.Sum(i => i.PaidAmount) != receivable.PaidAmount)
            throw new DomainRuleViolationException(
                $"La cuenta por cobrar {receivable.Id} tiene cuotas que no cuadran con su monto cobrado: no admite aplicar ni revertir anticipos."
            );
    }
}
