using ERP.Application.Modules.Payables.Exceptions;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Payables.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// SUPPLIER-PAYMENTS-POSTING-15D — traduce <c>SupplierPaymentConfirmedEvent</c> (Payables) a
/// <c>PostingFact</c> e invoca <c>IPostingEngine</c> — no crea <c>JournalEntry</c>, no contiene
/// lógica financiera (ADR-026 §8), mismo criterio que el resto de traductores. Deliberadamente NO
/// se llama <c>SupplierPaymentAppliedPostingTranslator</c> (nombre legacy bloqueado por
/// <c>PaymentsLegacyCleanupTests</c>) ni consume <c>SupplierPaymentAppliedEvent</c> (Finance,
/// pertenece al agregado <c>Payment</c> ya descartado) — este traductor es del agregado
/// <c>SupplierPayment</c> independiente (SUPPLIER-PAYMENTS-AUDIT-15A).
///
/// Asiento estricto de un solo lado fijo: Debe Cuentas por Pagar por el total del pago (vía
/// <c>PostingRule</c> normal, <c>PostingAmountKind.GrandTotal</c>) y un Haber por cada
/// <c>SupplierPaymentMethodLine</c> (vía <see cref="PostingAllocation"/>, <c>AccountNature.Credit</c>,
/// cardinalidad variable — 1 medio, 1 crédito; 2 medios, 2 créditos; nunca por cuota, el pago
/// postea por medio de pago). Mismo criterio de "no warning silencioso" que
/// <c>ExpenseDocumentConfirmedPostingTranslator</c>: si el posting falla, lanza
/// <see cref="SupplierPaymentPostingFailedException"/> — nunca solo un log — para que la
/// transacción completa del registro del pago se revierta (ADR-026 §8: Publish() ocurre dentro de
/// <c>ErpDbContext.SaveChangesAsync</c>, antes del commit).
/// </summary>
public sealed class SupplierPaymentConfirmedPostingTranslator
    : INotificationHandler<SupplierPaymentConfirmedEvent>
{
    private const string SourceModuleName = "Payables";
    private const string FactTypeName = "SupplierPaymentConfirmed";

    private readonly IPostingEngine _postingEngine;
    private readonly ICompanyFinancialDestinationRepository _financialDestinations;

    public SupplierPaymentConfirmedPostingTranslator(
        IPostingEngine postingEngine,
        ICompanyFinancialDestinationRepository financialDestinations
    )
    {
        _postingEngine = postingEngine;
        _financialDestinations = financialDestinations;
    }

    public async Task Handle(SupplierPaymentConfirmedEvent e, CancellationToken ct)
    {
        var tenantId = e.TenantId!.Value;
        var allocations = new List<PostingAllocation>();

        foreach (var methodLine in e.MethodLines)
        {
            var destination = await _financialDestinations.GetByIdAsync(
                tenantId,
                methodLine.FinancialDestinationId,
                ct
            );
            if (destination is null || destination.CompanyId != e.CompanyId)
                throw new SupplierPaymentPostingFailedException(
                    $"El destino financiero {methodLine.FinancialDestinationId} no existe o no "
                        + "pertenece a esta empresa."
                );
            if (!destination.IsActive)
                throw new SupplierPaymentPostingFailedException(
                    $"El destino financiero {methodLine.FinancialDestinationId} no está activo."
                );
            if (destination.AccountingAccountId == Guid.Empty)
                throw new SupplierPaymentPostingFailedException(
                    $"El destino financiero {methodLine.FinancialDestinationId} no tiene una "
                        + "cuenta contable configurada."
                );

            allocations.Add(
                new PostingAllocation(
                    destination.AccountingAccountId,
                    methodLine.Amount,
                    AccountNature.Credit
                )
            );
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
            Allocations: allocations
        );

        var result = await _postingEngine.PostAsync(fact, ct);

        if (!result.IsSuccess)
            throw new SupplierPaymentPostingFailedException(
                result.Error ?? "No se pudo contabilizar el pago a proveedor.",
                result.Code
            );
    }
}
