using ERP.Domain.Modules.Purchases.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// ACCOUNTING-CREDIT-NOTES-POSTING-08: primer handler contable de
/// <see cref="PurchaseCreditNoteAuthorizedEvent"/> — el evento existía desde FLOW-READY-02C §4.3
/// deliberadamente sin traductor ("punto de extensión inerte... una fase futura que decida darle
/// efecto contable debe hacerlo explícitamente"); este ticket es esa decisión explícita, sin
/// modificar el evento en sí.
/// </summary>
/// <remarks>
/// Solo cubre <c>PurchaseCreditNoteApplicationType.Discount</c> — el único tipo que llega a
/// <c>PurchaseCreditNote.Authorize()</c> (el tipo <c>Return</c> nunca se autoriza aquí, ver
/// remarks de la entidad; su efecto contable es exclusivamente
/// <see cref="PurchaseReturnAuthorizedPostingTranslator"/>, ya existente — este traductor nunca
/// duplica esa contabilización). Sin efecto de inventario (la entidad nunca llama
/// <c>IStockRepository</c>) — <c>PostingFact.HistoricalCostTotal</c>/<c>CostVariance*</c> quedan en
/// null. <c>SupplierCreditAmount</c> también queda en null: <c>Authorize()</c> bloquea
/// estructuralmente que el total exceda el saldo pendiente, así que nunca hay excedente que
/// desborde a un <c>SupplierCredit</c> (a diferencia de <c>PurchaseReturn</c>).
/// </remarks>
/// <remarks>
/// ACCOUNTING-PURCHASE-CREDIT-NOTE-ICE-08B: <c>TotalIce</c> ahora transporta
/// <see cref="PurchaseCreditNoteAuthorizedEvent.IceAmount"/> (antes fijo en 0m — brecha ya
/// cerrada). Nunca recalcula el ICE: usa el monto ya congelado por
/// <c>PurchaseCreditNote.RecalculateTotals()</c>. Si la NC no tiene componente ICE,
/// <c>IceAmount</c> es 0m y la línea de <c>PostingAmountKind.TaxIce</c> de la
/// <c>PostingRule</c> (si el admin la configuró) se omite automáticamente —
/// <c>JournalFactory</c> nunca contabiliza una línea en monto cero (ver su propio doc comment) —
/// así que la MISMA regla sirve para NC con y sin ICE, sin lógica condicional aquí. Sin cuenta ICE
/// hardcodeada: qué cuenta recibe ese crédito es 100% configuración de <c>PostingRule</c>
/// (administrada por Company), nunca código — si no existe esa línea configurada, el ICE
/// simplemente no se contabiliza (brecha de configuración del admin, no de este traductor, ver
/// entregable).
/// </remarks>
public sealed class PurchaseCreditNoteAuthorizedPostingTranslator
    : INotificationHandler<PurchaseCreditNoteAuthorizedEvent>
{
    private const string SourceModuleName = "Purchases";
    private const string FactTypeName = "PurchaseCreditNoteAuthorized";

    private readonly IPostingEngine _postingEngine;
    private readonly ILogger<PurchaseCreditNoteAuthorizedPostingTranslator> _logger;

    public PurchaseCreditNoteAuthorizedPostingTranslator(
        IPostingEngine postingEngine,
        ILogger<PurchaseCreditNoteAuthorizedPostingTranslator> logger
    )
    {
        _postingEngine = postingEngine;
        _logger = logger;
    }

    public async Task Handle(PurchaseCreditNoteAuthorizedEvent e, CancellationToken ct)
    {
        var fact = new PostingFact(
            e.TenantId!.Value,
            e.CompanyId,
            SourceModuleName,
            FactTypeName,
            e.PurchaseCreditNoteId,
            DateOnly.FromDateTime(e.OccurredOn),
            Subtotal: e.Subtotal,
            TotalVat: e.VatAmount,
            TotalIce: e.IceAmount,
            TotalDiscount: 0m,
            GrandTotal: e.TotalAmount,
            AppliedToPayableAmount: e.AppliedToPayableAmount
        );

        var result = await _postingEngine.PostAsync(fact, ct);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Posting failed for PurchaseCreditNote {PurchaseCreditNoteId} ({CreditNoteNumber}): {Code} — {Error}",
                e.PurchaseCreditNoteId,
                e.CreditNoteNumber,
                result.Code,
                result.Error
            );
        }
    }
}
