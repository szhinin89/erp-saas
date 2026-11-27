using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Finance.Events;
using ERP.Domain.Modules.Finance.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// CUSTOMER-ADVANCES-V1 (ADR-052 §10, TA-09) — hechos contables de los anticipos de clientes. Cada
/// movimiento contabilizable registra su obligación durable en la transacción de la operación
/// (identidad = el movimiento, CA-INV-08) con la fecha efectiva del movimiento; ni Caja ni
/// CollectionApplied contabilizan estos efectos.
/// </summary>
internal static class CustomerAdvancePosting
{
    public const string SourceModuleName = "Finance";

    public static AccountingPostingRequest Request(
        string factType,
        CustomerAdvanceEventBase e,
        IReadOnlyList<AccountingPostingDestination>? destinations = null
    )
    {
        var references = new Dictionary<string, Guid>
        {
            ["CustomerAdvanceId"] = e.CustomerAdvanceId,
            ["CustomerId"] = e.CustomerId,
        };
        if (e.ReceivableId is { } receivableId)
            references["ReceivableId"] = receivableId;

        return new AccountingPostingRequest(
            e.TenantId!.Value,
            e.CompanyId,
            SourceModuleName,
            factType,
            e.MovementId,
            e.Reference,
            e.EffectiveDate,
            e.EffectiveDate,
            new AccountingPostingPayload(
                0m,
                0m,
                0m,
                0m,
                e.Amount,
                Destinations: destinations,
                References: references
            )
        );
    }

    /// <summary>
    /// La caja o cuenta bancaria real sustituye, en cada intento, la cuenta de la línea GrandTotal
    /// de la regla en el lado indicado.
    /// </summary>
    public static IReadOnlyList<AccountingPostingDestination> Destination(
        Guid? companyBankAccountId,
        Guid? cashRegisterId,
        decimal amount,
        AccountNature side
    ) =>
        [
            AccountingPostingDestination.FromCashOrBank(
                companyBankAccountId,
                cashRegisterId,
                amount,
                side,
                PostingAmountKind.GrandTotal
            ),
        ];
}

/// <summary>Finance/CustomerAdvanceReceived: Debe caja o banco destino / Haber anticipos de clientes.</summary>
public sealed class CustomerAdvanceReceivedPostingTranslator(AccountingPostingRecorder recorder)
    : INotificationHandler<CustomerAdvanceReceivedEvent>
{
    public const string SourceModuleName = CustomerAdvancePosting.SourceModuleName;
    public const string FactTypeName = "CustomerAdvanceReceived";

    public Task Handle(CustomerAdvanceReceivedEvent e, CancellationToken ct) =>
        recorder.RecordAsync(
            CustomerAdvancePosting.Request(
                FactTypeName,
                e,
                CustomerAdvancePosting.Destination(
                    e.CompanyBankAccountId,
                    e.CashRegisterId,
                    e.Amount,
                    AccountNature.Debit
                )
            ),
            ct
        );
}

/// <summary>
/// Finance/CustomerAdvanceApplied: Debe anticipos de clientes / Haber cuentas por cobrar. Una
/// obligación por línea aplicada.
/// </summary>
public sealed class CustomerAdvanceAppliedPostingTranslator(AccountingPostingRecorder recorder)
    : INotificationHandler<CustomerAdvanceAppliedEvent>
{
    public const string SourceModuleName = CustomerAdvancePosting.SourceModuleName;
    public const string FactTypeName = "CustomerAdvanceApplied";

    public Task Handle(CustomerAdvanceAppliedEvent e, CancellationToken ct) =>
        recorder.RecordAsync(CustomerAdvancePosting.Request(FactTypeName, e), ct);
}

/// <summary>
/// Finance/CustomerAdvanceRefunded: Debe anticipos de clientes / Haber caja o banco del que sale el
/// dinero. El destino es el de la transacción de devolución (puede diferir del ingreso), que debe
/// persistirse en la misma operación que el movimiento.
/// </summary>
public sealed class CustomerAdvanceRefundedPostingTranslator(
    AccountingPostingRecorder recorder,
    ICustomerAdvanceRefundTransactionRepository transactions
) : INotificationHandler<CustomerAdvanceRefundedEvent>
{
    public const string SourceModuleName = CustomerAdvancePosting.SourceModuleName;
    public const string FactTypeName = "CustomerAdvanceRefunded";

    public async Task Handle(CustomerAdvanceRefundedEvent e, CancellationToken ct)
    {
        var transaction =
            await transactions.GetByMovementIdAsync(e.TenantId!.Value, e.MovementId, ct)
            ?? throw new DomainRuleViolationException(
                "No se encontró la transacción de la devolución del anticipo."
            );

        await recorder.RecordAsync(
            CustomerAdvancePosting.Request(
                FactTypeName,
                e,
                CustomerAdvancePosting.Destination(
                    transaction.CompanyBankAccountId,
                    transaction.CashRegisterId,
                    transaction.Amount,
                    AccountNature.Credit
                )
            ),
            ct
        );
    }
}

/// <summary>
/// Espejo exacto del asiento de la aplicación revertida; sin asiento, la obligación pendiente se
/// cancela. No resuelve reglas ni cuentas vigentes.
/// </summary>
public sealed class CustomerAdvanceApplicationReversedPostingTranslator(
    IAccountingReversalRecorder reversals
) : INotificationHandler<CustomerAdvanceApplicationReversedEvent>
{
    public Task Handle(CustomerAdvanceApplicationReversedEvent e, CancellationToken ct) =>
        reversals.RecordSourceAsync(
            e.TenantId!.Value,
            e.CompanyId,
            CustomerAdvancePosting.SourceModuleName,
            CustomerAdvanceAppliedPostingTranslator.FactTypeName,
            e.ReversalOfMovementId!.Value,
            "Reversa de aplicación de anticipo de cliente",
            e.OccurredOn,
            ct
        );
}

/// <summary>Espejo exacto del asiento de la devolución revertida, o cancelación de su obligación.</summary>
public sealed class CustomerAdvanceRefundReversedPostingTranslator(
    IAccountingReversalRecorder reversals
) : INotificationHandler<CustomerAdvanceRefundReversedEvent>
{
    public Task Handle(CustomerAdvanceRefundReversedEvent e, CancellationToken ct) =>
        reversals.RecordSourceAsync(
            e.TenantId!.Value,
            e.CompanyId,
            CustomerAdvancePosting.SourceModuleName,
            CustomerAdvanceRefundedPostingTranslator.FactTypeName,
            e.ReversalOfMovementId!.Value,
            "Reversa de devolución de anticipo de cliente",
            e.OccurredOn,
            ct
        );
}

/// <summary>Reversa del ingreso: espejo del asiento de la recepción, o cancelación de su obligación.</summary>
public sealed class CustomerAdvanceIncomeReversedPostingTranslator(
    IAccountingReversalRecorder reversals
) : INotificationHandler<CustomerAdvanceIncomeReversedEvent>
{
    public Task Handle(CustomerAdvanceIncomeReversedEvent e, CancellationToken ct) =>
        reversals.RecordSourceAsync(
            e.TenantId!.Value,
            e.CompanyId,
            CustomerAdvancePosting.SourceModuleName,
            CustomerAdvanceReceivedPostingTranslator.FactTypeName,
            e.ReversalOfMovementId!.Value,
            "Reversa del ingreso de anticipo de cliente",
            e.OccurredOn,
            ct
        );
}

/// <summary>
/// Anulación administrativa (ADR-052 §6): solo cancela la obligación sin asiento. Con un asiento
/// publicado rechaza toda la operación, que nunca se resuelve con un espejo.
/// </summary>
public sealed class CustomerAdvanceCancelledPostingTranslator(
    AccountingPostingCancellation cancellation
) : INotificationHandler<CustomerAdvanceCancelledEvent>
{
    public const string PostedEntryMessage =
        "El anticipo ya está contabilizado: no admite anulación administrativa. Corresponde reversar el ingreso o devolverlo.";

    public async Task Handle(CustomerAdvanceCancelledEvent e, CancellationToken ct)
    {
        var posted = await cancellation.PrepareAsync(
            e.TenantId!.Value,
            e.CompanyId,
            CustomerAdvancePosting.SourceModuleName,
            CustomerAdvanceReceivedPostingTranslator.FactTypeName,
            e.ReversalOfMovementId!.Value,
            ct
        );
        if (posted is not null)
            throw new DomainRuleViolationException(PostedEntryMessage);
    }
}
