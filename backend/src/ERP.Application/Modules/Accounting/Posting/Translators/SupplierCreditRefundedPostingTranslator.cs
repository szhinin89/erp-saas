using ERP.Application.Modules.Finance.Exceptions;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Purchases.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce <see cref="SupplierCreditRefundedEvent"/> (P0-02 Fase 8) al hecho contable del
/// reembolso: el proveedor devuelve el saldo a favor → Debe Caja/Banco que recibe el dinero, Haber
/// "1.1.03.004 Anticipos a proveedores" (reduce el saldo a favor reconocido como activo).
///
/// ZH-SUPPLIER-CREDIT-REFUND-POSTING-02D-B — reemplaza la desviación anterior
/// (<c>FactType="SupplierCreditRefunded:{DestinationCodeSnapshot}"</c>, una <c>PostingRule</c> por
/// cada caja/banco que ninguna company tenía sembrada → "RULE_NOT_FOUND" + warning silencioso, el
/// dinero entraba sin asiento). Ahora usa el patrón vigente de <c>SupplierPaymentConfirmed</c>:
/// FactType canónico único <c>"Purchases"/"SupplierCreditRefunded"</c> con UNA línea fija (Haber
/// Anticipos, <see cref="PostingAmountKind.GrandTotal"/>) y el Debe dinámico vía
/// <see cref="PostingAllocation"/> contra
/// <see cref="SupplierCreditRefundTransaction.AccountingAccountId"/> — la cuenta de la caja/banco
/// congelada al registrar el reembolso (§6.4bis), nunca resuelta de nuevo aquí. Fail-closed: si no
/// hay asiento lanza <see cref="SupplierCreditRefundPostingFailedException"/> y la transacción
/// completa del reembolso se revierte (nunca solo un log).
/// </summary>
public sealed class SupplierCreditRefundedPostingTranslator
    : INotificationHandler<SupplierCreditRefundedEvent>
{
    internal const string SourceModuleName = "Purchases";
    internal const string FactTypeName = "SupplierCreditRefunded";

    private readonly IPostingEngine _postingEngine;
    private readonly ISupplierCreditRefundTransactionRepository _txRepo;

    public SupplierCreditRefundedPostingTranslator(
        IPostingEngine postingEngine,
        ISupplierCreditRefundTransactionRepository txRepo
    )
    {
        _postingEngine = postingEngine;
        _txRepo = txRepo;
    }

    public async Task Handle(SupplierCreditRefundedEvent e, CancellationToken ct)
    {
        var tenantId = e.TenantId!.Value;
        var transaction =
            await _txRepo.GetBySupplierCreditMovementIdAsync(
                tenantId,
                e.SupplierCreditMovementId,
                ct
            )
            ?? throw new SupplierCreditRefundPostingFailedException(
                "No se encontró la transacción del reembolso: no se puede contabilizar."
            );

        await SupplierCreditRefundPosting.PostAsync(
            _postingEngine,
            tenantId,
            e.CompanyId,
            FactTypeName,
            e.SupplierCreditMovementId,
            transaction,
            destinationNature: AccountNature.Debit,
            "No se pudo contabilizar el reembolso del saldo a favor del proveedor.",
            ct
        );
    }
}

/// <summary>
/// ZH-SUPPLIER-CREDIT-REFUND-POSTING-02D-B — construcción compartida del <see cref="PostingFact"/>
/// de reembolso/reversa (mismo hecho, naturaleza de la línea dinámica invertida). La línea de
/// Anticipos la aporta la <c>PostingRule</c> canónica (<see cref="PostingAmountKind.GrandTotal"/>);
/// la de Caja/Banco es la allocation con la cuenta congelada de la transacción.
/// </summary>
internal static class SupplierCreditRefundPosting
{
    public static async Task PostAsync(
        IPostingEngine postingEngine,
        Guid tenantId,
        Guid companyId,
        string factType,
        Guid sourceEventId,
        SupplierCreditRefundTransaction transaction,
        AccountNature destinationNature,
        string genericError,
        CancellationToken ct
    )
    {
        var fact = new PostingFact(
            tenantId,
            companyId,
            SupplierCreditRefundedPostingTranslator.SourceModuleName,
            factType,
            sourceEventId,
            transaction.EffectiveDate,
            Subtotal: 0m,
            TotalVat: 0m,
            TotalIce: 0m,
            TotalDiscount: 0m,
            GrandTotal: transaction.Amount,
            Allocations:
            [
                new PostingAllocation(
                    transaction.AccountingAccountId,
                    transaction.Amount,
                    destinationNature,
                    transaction.DestinationNameSnapshot
                ),
            ]
        );

        var result = await postingEngine.PostAsync(fact, ct);
        if (!result.IsSuccess)
            throw new SupplierCreditRefundPostingFailedException(
                result.Error ?? genericError,
                result.Code
            );
    }
}
