using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Purchases.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce <see cref="SupplierCreditRefundReversedEvent"/> (P0-02 Fase 8) al hecho contable
/// reverso del de <see cref="SupplierCreditRefundedPostingTranslator"/> — usa la MISMA cuenta
/// contable congelada heredada por <c>SupplierCreditRefundTransaction.CreateReversal</c> (nunca la
/// vigente del destino, §19.1ter), resuelta vía el mismo mecanismo de lectura por repositorio. El
/// <c>PostingRule</c> configurado para <c>FactType="SupplierCreditRefundReversed:{destino}"</c>
/// invierte débito/crédito respecto al de reembolso — responsabilidad de administración contable,
/// no de este traductor. Ver desviación documentada en <see cref="SupplierCreditRefundedPostingTranslator"/>.
/// </summary>
public sealed class SupplierCreditRefundReversedPostingTranslator
    : INotificationHandler<SupplierCreditRefundReversedEvent>
{
    private const string SourceModuleName = "Purchases";

    private readonly IPostingEngine _postingEngine;
    private readonly ISupplierCreditRefundTransactionRepository _txRepo;
    private readonly ILogger<SupplierCreditRefundReversedPostingTranslator> _logger;

    public SupplierCreditRefundReversedPostingTranslator(
        IPostingEngine postingEngine,
        ISupplierCreditRefundTransactionRepository txRepo,
        ILogger<SupplierCreditRefundReversedPostingTranslator> logger
    )
    {
        _postingEngine = postingEngine;
        _txRepo = txRepo;
        _logger = logger;
    }

    public async Task Handle(SupplierCreditRefundReversedEvent e, CancellationToken ct)
    {
        var transaction = await _txRepo.GetBySupplierCreditMovementIdAsync(
            e.TenantId!.Value,
            e.SupplierCreditMovementId,
            ct
        );
        if (transaction is null)
        {
            _logger.LogWarning(
                "SupplierCreditRefundTransaction no encontrada para el movimiento {MovementId} — posting omitido.",
                e.SupplierCreditMovementId
            );
            return;
        }

        var fact = new PostingFact(
            e.TenantId!.Value,
            e.CompanyId,
            SourceModuleName,
            $"SupplierCreditRefundReversed:{transaction.FinancialDestinationCodeSnapshot}",
            e.SupplierCreditMovementId,
            transaction.EffectiveDate,
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
                "Posting failed for SupplierCredit {SupplierCreditId} refund reversal {MovementId}: {Code} — {Error}",
                e.SupplierCreditId,
                e.SupplierCreditMovementId,
                result.Code,
                result.Error
            );
        }
    }
}
