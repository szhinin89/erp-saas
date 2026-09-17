using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Finance.Events;
using ERP.Domain.Modules.Finance.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// Traduce CollectionAppliedEvent (Finance/Payment) a PostingFact e invoca IPostingEngine — no
/// crea JournalEntry, no contiene lógica financiera (ADR-026 §8, Fase 5.6.1). Un cobro no tiene
/// desglose de impuestos: Subtotal/TotalVat/TotalIce/TotalDiscount van en cero deliberadamente (no
/// son "montos inventados" — un cobro real no tiene esos componentes) y GrandTotal transporta el
/// monto cobrado, mismo campo genérico que ya usan los traductores de Ventas/Compras.
///
/// FINANCIAL-DESTINATION-TO-BANK-ACCOUNT-MIGRATION-01 — si el cobro especificó una cuenta
/// bancaria (<see cref="CollectionAppliedEvent.CompanyBankAccountId"/>) o una caja
/// (<see cref="CollectionAppliedEvent.CashRegisterId"/>), lee su <c>AccountingAccountId</c> y lo
/// pasa como override de la línea Debe de la PostingRule (el lado "caja/banco" de un cobro, nunca
/// el lado CxC) — no es "resolver una regla contable" (eso sigue siendo exclusivo del Posting
/// Engine), es leer tal cual un dato ya elegido por el usuario en Finance, mismo principio que
/// Subtotal/TotalVat arriba. An explicit bank/cash selection fails closed.
/// </summary>
public sealed class CollectionAppliedPostingTranslator
    : INotificationHandler<CollectionAppliedEvent>
{
    private const string SourceModuleName = "Finance";
    private const string FactTypeName = "CollectionApplied";

    private readonly IPostingEngine _postingEngine;
    private readonly ICompanyBankAccountRepository _bankAccounts;
    private readonly ICashRegisterRepository _cashRegisters;
    private readonly ILogger<CollectionAppliedPostingTranslator> _logger;

    public CollectionAppliedPostingTranslator(
        IPostingEngine postingEngine,
        ICompanyBankAccountRepository bankAccounts,
        ICashRegisterRepository cashRegisters,
        ILogger<CollectionAppliedPostingTranslator> logger
    )
    {
        _postingEngine = postingEngine;
        _bankAccounts = bankAccounts;
        _cashRegisters = cashRegisters;
        _logger = logger;
    }

    public async Task Handle(CollectionAppliedEvent e, CancellationToken ct)
    {
        if (e.CompanyBankAccountId is not null && e.CashRegisterId is not null)
            throw new InvalidOperationException("A collection cannot target both bank and cash.");
        Guid? overrideAccountId = null;
        if (e.CompanyBankAccountId is { } bankAccountId)
        {
            var bank = await _bankAccounts.GetByIdAsync(e.TenantId!.Value, bankAccountId, ct);
            if (bank is null || bank.TenantId != e.TenantId || bank.CompanyId != e.CompanyId || !bank.IsActive)
                throw new InvalidOperationException("The selected bank account is unavailable for this company.");
            overrideAccountId = bank.AccountingAccountId;
        }
        else if (e.CashRegisterId is { } cashRegisterId)
        {
            var cash = await _cashRegisters.GetByIdAsync(e.TenantId!.Value, cashRegisterId, ct);
            if (cash is null || cash.TenantId != e.TenantId || cash.CompanyId != e.CompanyId || !cash.IsActive || cash.AccountingAccountId is null)
                throw new InvalidOperationException("The selected cash register is unavailable or has no accounting account.");
            overrideAccountId = cash.AccountingAccountId;
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
            OverrideAccountNature: overrideAccountId is null ? null : AccountNature.Debit,
            OverrideAccountId: overrideAccountId
        );

        var result = await _postingEngine.PostAsync(fact, ct);

        if (!result.IsSuccess)
        {
            if (overrideAccountId is not null)
                throw new InvalidOperationException($"Collection posting failed: {result.Code} - {result.Error}");
            _logger.LogWarning(
                "Posting failed for Collection {PaymentId}: {Code} — {Error}",
                e.PaymentId,
                result.Code,
                result.Error
            );
        }
    }
}
