using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Purchases.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce <see cref="SupplierCreditRefundedEvent"/> (P0-02 Fase 8) al hecho contable de §19.1ter
/// (débito <c>SupplierCreditRefundTransaction.AccountingAccountId</c> congelado, crédito "Crédito a
/// favor frente a proveedores"). El evento no transporta la cuenta congelada — este traductor la
/// resuelve mediante <see cref="ISupplierCreditRefundTransactionRepository.GetBySupplierCreditMovementIdAsync"/>
/// (misma técnica de lectura vía repositorio ya usada en <c>SupplierCreditAuditHandler</c>, Fase 7).
///
/// Desviación documentada respecto al mecanismo genérico de <c>PostingRule</c> (una cuenta fija
/// por <c>FactType</c>): dado que <see cref="Domain.Modules.Finance.Entities.CompanyFinancialDestination"/>
/// puede tener una cuenta contable distinta por cada destino (banco/caja) y el Posting Engine
/// actual (<c>PostingRuleLine.AccountId</c>) no admite una cuenta dinámica por transacción sin
/// modificar infraestructura FROZEN (<c>PostingEngine.cs</c>/<c>PostingRule.cs</c>/<c>JournalFactory.cs</c>,
/// fuera del alcance autorizado de esta fase), el <c>FactType</c> incorpora el código del destino
/// financiero (<c>"SupplierCreditRefunded:{FinancialDestinationCodeSnapshot}"</c>) — permite a cada
/// tenant configurar una <c>PostingRule</c> por destino con su propia cuenta de débito, sin tocar
/// ningún archivo de la infraestructura de Posting. Limitación conocida: si la cuenta contable del
/// destino cambia (<c>ChangeAccountingAccount</c>) DESPUÉS de que existan reembolsos ya
/// contabilizados con la <c>PostingRule</c> anterior, el administrador debe actualizar esa
/// <c>PostingRule</c> para que los reembolsos NUEVOS usen la cuenta correcta — los asientos ya
/// posteados no se ven afectados (inmutables), consistente con el congelamiento histórico exigido
/// por §6.4bis a nivel de <see cref="Domain.Modules.Finance.Entities.SupplierCreditRefundTransaction"/>.
/// </summary>
public sealed class SupplierCreditRefundedPostingTranslator
    : INotificationHandler<SupplierCreditRefundedEvent>
{
    private const string SourceModuleName = "Purchases";

    private readonly IPostingEngine _postingEngine;
    private readonly ISupplierCreditRefundTransactionRepository _txRepo;
    private readonly ILogger<SupplierCreditRefundedPostingTranslator> _logger;

    public SupplierCreditRefundedPostingTranslator(
        IPostingEngine postingEngine,
        ISupplierCreditRefundTransactionRepository txRepo,
        ILogger<SupplierCreditRefundedPostingTranslator> logger
    )
    {
        _postingEngine = postingEngine;
        _txRepo = txRepo;
        _logger = logger;
    }

    public async Task Handle(SupplierCreditRefundedEvent e, CancellationToken ct)
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
            $"SupplierCreditRefunded:{transaction.FinancialDestinationCodeSnapshot}",
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
                "Posting failed for SupplierCredit {SupplierCreditId} refund {MovementId}: {Code} — {Error}",
                e.SupplierCreditId,
                e.SupplierCreditMovementId,
                result.Code,
                result.Error
            );
        }
    }
}
