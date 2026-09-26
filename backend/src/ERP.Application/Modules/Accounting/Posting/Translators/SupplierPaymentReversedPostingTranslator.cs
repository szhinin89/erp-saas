using ERP.Application.Modules.Payables.Exceptions;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Payables.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// SUPPLIER-PAYMENTS-REVERSE-16 — traduce <c>SupplierPaymentReversedEvent</c> (Payables) a
/// <c>PostingFact</c> e invoca <c>IPostingEngine</c> — no crea <c>JournalEntry</c>, no contiene
/// lógica financiera (ADR-026 §8), mismo criterio que
/// <see cref="SupplierPaymentConfirmedPostingTranslator"/>. Este nombre de clase reemplaza al
/// traductor legacy homónimo de Finance (<c>ERP.Domain.Modules.Finance.Events.SupplierPaymentReversedEvent</c>,
/// código muerto desde PAYABLES-PAYMENTS-LEGACY-CLEANUP-14 — nada lo disparaba en la práctica, se
/// eliminó junto con su test en este ticket) — este es el único traductor real para reversos de
/// pago a proveedor.
///
/// Asiento inverso exacto del de confirmación: Haber Cuentas por Pagar por el total (vía
/// <c>PostingRule</c> normal, <c>PostingAmountKind.GrandTotal</c>, <c>AccountNature.Credit</c>) y un
/// Debe por cada <c>SupplierPaymentMethodLine</c> original (vía <see cref="PostingAllocation"/>,
/// <c>AccountNature.Debit</c> — cardinalidad variable, igual criterio "postea por medio, no por
/// cuota" que la confirmación). Mismo criterio de "no warning silencioso": si el posting inverso
/// falla, lanza <see cref="SupplierPaymentPostingFailedException"/> para que la transacción completa
/// de la reversa se revierta (el pago sigue Confirmed, los saldos de
/// <c>AccountsPayableInstallment</c> no cambian, no queda asiento parcial).
///
/// ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C (ADR-035) — espejo exacto de la confirmación: Haber
/// CxP por lo aplicado (<c>AppliedToPayable</c>) + Haber "Anticipos a proveedores" por el remanente
/// (<c>SupplierCredit</c>), mismo guard fail-closed si la regla no declara la línea de anticipo.
/// </summary>
public sealed class SupplierPaymentReversedPostingTranslator
    : INotificationHandler<SupplierPaymentReversedEvent>
{
    private const string SourceModuleName = "Payables";
    private const string FactTypeName = "SupplierPaymentReversed";

    private readonly IPostingEngine _postingEngine;
    private readonly ICompanyBankAccountRepository _bankAccounts;
    private readonly ICashRegisterRepository _cashRegisters;

    public SupplierPaymentReversedPostingTranslator(
        IPostingEngine postingEngine,
        ICompanyBankAccountRepository bankAccounts,
        ICashRegisterRepository cashRegisters
    )
    {
        _postingEngine = postingEngine;
        _bankAccounts = bankAccounts;
        _cashRegisters = cashRegisters;
    }

    public async Task Handle(SupplierPaymentReversedEvent e, CancellationToken ct)
    {
        var tenantId = e.TenantId!.Value;
        var allocations = new List<PostingAllocation>();

        foreach (var methodLine in e.MethodLines)
        {
            var accountId = await _bankAccounts.ResolveAccountingAccountIdAsync(
                _cashRegisters,
                tenantId,
                e.CompanyId,
                methodLine,
                ct
            );

            allocations.Add(new PostingAllocation(accountId, methodLine.Amount, AccountNature.Debit));
        }

        var fact = new PostingFact(
            tenantId,
            e.CompanyId,
            SourceModuleName,
            FactTypeName,
            e.SupplierPaymentId,
            e.PaymentDate,
            Subtotal: 0m,
            TotalVat: 0m,
            TotalIce: 0m,
            TotalDiscount: 0m,
            GrandTotal: e.TotalAmount,
            AppliedToPayableAmount: e.AppliedAmount,
            SupplierCreditAmount: e.UnappliedAmount,
            Allocations: allocations
        );

        await SupplierPaymentAdvancePostingGuard.EnsureAdvanceLineConfiguredAsync(
            _postingEngine,
            tenantId,
            e.CompanyId,
            SourceModuleName,
            FactTypeName,
            e.UnappliedAmount,
            ct
        );

        var result = await _postingEngine.PostAsync(fact, ct);

        if (!result.IsSuccess)
            throw new SupplierPaymentPostingFailedException(
                result.Error ?? "No se pudo contabilizar la reversa del pago a proveedor.",
                result.Code
            );
    }
}
