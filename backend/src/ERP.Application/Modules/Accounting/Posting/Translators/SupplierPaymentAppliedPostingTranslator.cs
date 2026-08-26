using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Finance.Events;
using ERP.Domain.Modules.Finance.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce SupplierPaymentAppliedEvent (Finance/Payment) a PostingFact e invoca IPostingEngine —
/// no crea JournalEntry, no contiene lógica financiera (ADR-026 §8, Fase 5.6.1). Mismo criterio
/// que <see cref="CollectionAppliedPostingTranslator"/>: sin desglose de impuestos
/// (Subtotal/TotalVat/TotalIce/TotalDiscount en cero, no inventados), GrandTotal transporta el
/// monto pagado.
///
/// ACCOUNTING-PAYMENT-METHOD-ACCOUNT-MAPPING-14 — mismo mecanismo de override que
/// <see cref="CollectionAppliedPostingTranslator"/>, aplicado al lado Haber de la PostingRule (el
/// lado "caja/banco" de un pago a proveedor, nunca el lado CxP).
/// </summary>
public sealed class SupplierPaymentAppliedPostingTranslator
    : INotificationHandler<SupplierPaymentAppliedEvent>
{
    private const string SourceModuleName = "Finance";
    private const string FactTypeName = "SupplierPaymentApplied";

    private readonly IPostingEngine _postingEngine;
    private readonly ICompanyFinancialDestinationRepository _financialDestinations;
    private readonly ILogger<SupplierPaymentAppliedPostingTranslator> _logger;

    public SupplierPaymentAppliedPostingTranslator(
        IPostingEngine postingEngine,
        ICompanyFinancialDestinationRepository financialDestinations,
        ILogger<SupplierPaymentAppliedPostingTranslator> logger
    )
    {
        _postingEngine = postingEngine;
        _financialDestinations = financialDestinations;
        _logger = logger;
    }

    public async Task Handle(SupplierPaymentAppliedEvent e, CancellationToken ct)
    {
        Guid? overrideAccountId = null;
        if (e.FinancialDestinationId is { } destinationId)
        {
            var destination = await _financialDestinations.GetByIdAsync(
                e.TenantId!.Value,
                destinationId,
                ct
            );
            if (destination is { IsActive: true })
            {
                overrideAccountId = destination.AccountingAccountId;
            }
            else
            {
                _logger.LogWarning(
                    "Financial destination {FinancialDestinationId} not found or inactive for "
                        + "SupplierPayment {PaymentId} — falling back to the PostingRule default account.",
                    destinationId,
                    e.PaymentId
                );
            }
        }

        var fact = new PostingFact(
            e.TenantId!.Value,
            e.CompanyId,
            SourceModuleName,
            FactTypeName,
            e.PaymentId,
            e.PaymentDate,
            0m,
            0m,
            0m,
            0m,
            e.Amount,
            OverrideAmountKind: overrideAccountId is null ? null : PostingAmountKind.GrandTotal,
            OverrideAccountNature: overrideAccountId is null ? null : AccountNature.Credit,
            OverrideAccountId: overrideAccountId
        );

        var result = await _postingEngine.PostAsync(fact, ct);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Posting failed for SupplierPayment {PaymentId}: {Code} — {Error}",
                e.PaymentId,
                result.Code,
                result.Error
            );
        }
    }
}
