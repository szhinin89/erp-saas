using ERP.Application.Modules.Finance.Exceptions;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Purchases.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce <see cref="SupplierCreditRefundReversedEvent"/> (P0-02 Fase 8) al asiento espejo exacto
/// del reembolso: Debe "1.1.03.004 Anticipos a proveedores" (el saldo a favor vuelve a existir),
/// Haber la MISMA Caja/Banco del reembolso original.
///
/// ZH-SUPPLIER-CREDIT-REFUND-POSTING-02D-B — FactType canónico único
/// <c>"Purchases"/"SupplierCreditRefundReversed"</c> (antes uno por destino, sin regla sembrada).
/// La cuenta de Caja/Banco sale de la transacción de reversa, que la HEREDA congelada del ingreso
/// original (<c>SupplierCreditRefundTransaction.CreateReversal</c>, §6.4quinquies) — nunca se
/// resuelve la cuenta vigente del destino. Fail-closed igual que
/// <see cref="SupplierCreditRefundedPostingTranslator"/>.
/// </summary>
public sealed class SupplierCreditRefundReversedPostingTranslator
    : INotificationHandler<SupplierCreditRefundReversedEvent>
{
    internal const string FactTypeName = "SupplierCreditRefundReversed";

    private readonly IPostingEngine _postingEngine;
    private readonly ISupplierCreditRefundTransactionRepository _txRepo;

    public SupplierCreditRefundReversedPostingTranslator(
        IPostingEngine postingEngine,
        ISupplierCreditRefundTransactionRepository txRepo
    )
    {
        _postingEngine = postingEngine;
        _txRepo = txRepo;
    }

    public async Task Handle(SupplierCreditRefundReversedEvent e, CancellationToken ct)
    {
        var tenantId = e.TenantId!.Value;
        var transaction =
            await _txRepo.GetBySupplierCreditMovementIdAsync(
                tenantId,
                e.SupplierCreditMovementId,
                ct
            )
            ?? throw new SupplierCreditRefundPostingFailedException(
                "No se encontró la transacción de la reversa del reembolso: no se puede contabilizar."
            );

        await SupplierCreditRefundPosting.PostAsync(
            _postingEngine,
            tenantId,
            e.CompanyId,
            FactTypeName,
            e.SupplierCreditMovementId,
            transaction,
            destinationNature: AccountNature.Credit,
            "No se pudo contabilizar la reversa del reembolso del saldo a favor del proveedor.",
            ct
        );
    }
}
