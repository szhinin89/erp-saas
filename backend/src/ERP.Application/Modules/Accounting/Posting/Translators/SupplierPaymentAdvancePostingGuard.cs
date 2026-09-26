using ERP.Application.Modules.Payables.Exceptions;
using ERP.Domain.Modules.Accounting.Enums;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C (ADR-035) — guard fail-closed compartido por
/// <see cref="SupplierPaymentConfirmedPostingTranslator"/>/<see cref="SupplierPaymentReversedPostingTranslator"/>:
/// un pago con remanente no aplicado solo se contabiliza si la <c>PostingRule</c> de la empresa
/// declara la línea <see cref="PostingAmountKind.SupplierCredit"/> ("Anticipos a proveedores").
/// Una regla con la forma previa a 02C (una sola línea <c>GrandTotal</c> contra CxP) debitaría CxP
/// por dinero que no se aplicó a ninguna cuota — nunca se permite en silencio. Mismo mecanismo ya
/// usado para IRBPNR (<see cref="IPostingEngine.IsAmountKindConfiguredAsync"/>).
/// </summary>
internal static class SupplierPaymentAdvancePostingGuard
{
    public static async Task EnsureAdvanceLineConfiguredAsync(
        IPostingEngine postingEngine,
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        string factType,
        decimal unappliedAmount,
        CancellationToken ct
    )
    {
        if (unappliedAmount <= 0)
            return;

        var configured = await postingEngine.IsAmountKindConfiguredAsync(
            tenantId,
            companyId,
            sourceModule,
            factType,
            PostingAmountKind.SupplierCredit,
            ct
        );
        if (!configured)
            throw new SupplierPaymentPostingFailedException(
                $"La regla contable {sourceModule}/{factType} no tiene configurada la línea de anticipos a proveedores: no se puede contabilizar un pago con saldo no aplicado. Actualice la regla contable antes de continuar."
            );
    }
}
