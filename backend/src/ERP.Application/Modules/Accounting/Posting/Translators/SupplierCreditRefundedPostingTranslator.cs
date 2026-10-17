using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Purchases.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce <see cref="SupplierCreditRefundedEvent"/> al hecho Purchases/SupplierCreditRefunded
/// (Debe Caja/Banco que recibe el dinero, Haber anticipos por la línea GrandTotal de la regla) y lo
/// registra como obligación durable en la transacción del reembolso (ACCOUNTING-CONTINUITY-04).
/// </summary>
/// <remarks>
/// El destino se persiste por identidad (caja o cuenta bancaria de la transacción) y su cuenta se
/// resuelve en cada intento con la configuración vigente: una caja sin cuenta deja la obligación
/// Failed, nunca rechaza el reembolso. Fecha: la efectiva de la transacción.
/// </remarks>
public sealed class SupplierCreditRefundedPostingTranslator(
    AccountingPostingRecorder recorder,
    ISupplierCreditRefundTransactionRepository transactions
) : INotificationHandler<SupplierCreditRefundedEvent>
{
    public const string SourceModuleName = "Purchases";
    public const string FactTypeName = "SupplierCreditRefunded";

    public async Task Handle(SupplierCreditRefundedEvent e, CancellationToken ct)
    {
        var tenantId = e.TenantId!.Value;
        var transaction =
            await transactions.GetBySupplierCreditMovementIdAsync(tenantId, e.SupplierCreditMovementId, ct)
            ?? throw new DomainRuleViolationException("No se encontró la transacción del reembolso.");

        await recorder.RecordAsync(
            new AccountingPostingRequest(
                tenantId,
                e.CompanyId,
                SourceModuleName,
                FactTypeName,
                e.SupplierCreditMovementId,
                transaction.ExternalReference,
                transaction.EffectiveDate,
                transaction.EffectiveDate,
                new AccountingPostingPayload(
                    0m,
                    0m,
                    0m,
                    0m,
                    transaction.Amount,
                    Destinations:
                    [
                        AccountingPostingDestination.FromCashOrBank(
                            transaction.CompanyBankAccountId,
                            transaction.CashRegisterId,
                            transaction.Amount,
                            AccountNature.Debit,
                            description: transaction.DestinationNameSnapshot
                        ),
                    ],
                    References: new Dictionary<string, Guid>
                    {
                        ["SupplierCreditId"] = e.SupplierCreditId,
                        ["RefundTransactionId"] = transaction.Id,
                    }
                )
            ),
            ct
        );
    }
}
