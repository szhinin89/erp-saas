using ERP.Application.Modules.Payables.Exceptions;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Payables.Events;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// FINANCIAL-DESTINATION-TO-BANK-ACCOUNT-MIGRATION-01 — resuelve la cuenta contable de un
/// <see cref="SupplierPaymentConfirmedMethodLine"/> (banco o caja) para
/// <see cref="SupplierPaymentConfirmedPostingTranslator"/>/<see cref="SupplierPaymentReversedPostingTranslator"/>
/// — misma validación fail-closed en ambos (nunca un warning silencioso, Payables exige asiento o
/// revierte la transacción completa).
/// </summary>
internal static class SupplierPaymentMethodLineAccountResolver
{
    public static async Task<Guid> ResolveAccountingAccountIdAsync(
        this ICompanyBankAccountRepository bankAccounts,
        ICashRegisterRepository cashRegisters,
        Guid tenantId,
        Guid companyId,
        SupplierPaymentConfirmedMethodLine methodLine,
        CancellationToken ct
    )
    {
        if (methodLine.CompanyBankAccountId is { } bankAccountId)
        {
            var bankAccount = await bankAccounts.GetByIdAsync(tenantId, bankAccountId, ct);
            if (bankAccount is null || bankAccount.CompanyId != companyId)
                throw new SupplierPaymentPostingFailedException(
                    $"La cuenta bancaria {bankAccountId} no existe o no pertenece a esta empresa."
                );
            if (!bankAccount.IsActive)
                throw new SupplierPaymentPostingFailedException(
                    $"La cuenta bancaria {bankAccountId} no está activa."
                );

            return bankAccount.AccountingAccountId;
        }

        if (methodLine.CashRegisterId is { } cashRegisterId)
        {
            var cashRegister = await cashRegisters.GetByIdAsync(tenantId, cashRegisterId, ct);
            if (cashRegister is null || cashRegister.CompanyId != companyId)
                throw new SupplierPaymentPostingFailedException(
                    $"La caja {cashRegisterId} no existe o no pertenece a esta empresa."
                );
            if (!cashRegister.IsActive)
                throw new SupplierPaymentPostingFailedException(
                    $"La caja {cashRegisterId} no está activa."
                );
            if (cashRegister.AccountingAccountId is not { } accountingAccountId)
                throw new SupplierPaymentPostingFailedException(
                    $"La caja {cashRegisterId} no tiene una cuenta contable configurada."
                );

            return accountingAccountId;
        }

        throw new SupplierPaymentPostingFailedException(
            "El medio de pago no tiene cuenta bancaria ni caja destino configurada."
        );
    }
}
