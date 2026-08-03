using ERP.Domain.Modules.Purchases.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce <see cref="SupplierCreditAppliedEvent"/> (P0-02 Fase 7) al hecho contable simple de
/// §19.1 (débito CxP destino, crédito "Crédito a favor frente a proveedores") e invoca
/// <see cref="IPostingEngine"/> — no crea <c>JournalEntry</c>, no resuelve cuentas, no contiene
/// lógica financiera propia (mismo criterio que <c>PurchaseReturnAuthorizedPostingTranslator</c>,
/// ADR-026 §8). Un único monto (<c>Amount</c>) se transporta en <c>GrandTotal</c> — sin necesidad
/// de campos nuevos en <see cref="PostingFact"/> (hecho de una sola línea por lado, igual que
/// otros consumidores del Posting Engine cuando no hay IVA/ICE involucrado).
/// </summary>
public sealed class SupplierCreditAppliedPostingTranslator
    : INotificationHandler<SupplierCreditAppliedEvent>
{
    private const string SourceModuleName = "Purchases";
    private const string FactTypeName = "SupplierCreditApplied";

    private readonly IPostingEngine _postingEngine;
    private readonly ILogger<SupplierCreditAppliedPostingTranslator> _logger;

    public SupplierCreditAppliedPostingTranslator(
        IPostingEngine postingEngine,
        ILogger<SupplierCreditAppliedPostingTranslator> logger
    )
    {
        _postingEngine = postingEngine;
        _logger = logger;
    }

    public async Task Handle(SupplierCreditAppliedEvent e, CancellationToken ct)
    {
        var fact = new PostingFact(
            e.TenantId!.Value,
            e.CompanyId,
            SourceModuleName,
            FactTypeName,
            e.SupplierCreditMovementId,
            DateOnly.FromDateTime(e.OccurredOn),
            Subtotal: 0m,
            TotalVat: 0m,
            TotalIce: 0m,
            TotalDiscount: 0m,
            GrandTotal: e.Amount
        );

        var result = await _postingEngine.PostAsync(fact, ct);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Posting failed for SupplierCredit {SupplierCreditId} application {MovementId}: {Code} — {Error}",
                e.SupplierCreditId,
                e.SupplierCreditMovementId,
                result.Code,
                result.Error
            );
        }
    }
}
