using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Companies;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Caja.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// ACCOUNTING-12C — hechos contables propios de Caja. Cada uno registra su obligación en la transacción
/// operativa con <see cref="AccountingPostingRecorder"/>; el intento corre después del commit y un fallo
/// contable nunca revierte el movimiento ni el cierre. Las cuentas se resuelven en cada intento: la caja
/// real de la sesión (<see cref="AccountingPostingDestination.CashRegister"/>), el motivo
/// (<see cref="AccountingPostingDestination.CashMovementReason"/>) y el banco destino
/// (<see cref="AccountingPostingDestination.BankAccount"/>). Fecha contable = día Company del instante
/// efectivo del movimiento o del cierre. Solo consume eventos con datos primitivos: Posting no depende
/// de <c>CashMovement</c>/<c>CashSession</c> (los demás efectos de caja los contabiliza su documento).
/// </summary>
internal static class CashPosting
{
    public const string SourceModuleName = "Caja";
    public const string BankDepositFactType = "BankDeposit";
    public const string SurplusFactType = "CashCountSurplus";
    public const string ShortageFactType = "CashCountShortage";

    public static AccountingPostingPayload Payload(decimal amount, params AccountingPostingDestination[] destinations) =>
        new(Subtotal: amount, TotalVat: 0m, TotalIce: 0m, TotalDiscount: 0m, GrandTotal: amount,
            Destinations: destinations);
}

/// <summary>
/// ManualIncome: Debe caja / Haber cuenta del motivo. ManualExpense y Withdrawal (retiro genérico,
/// nunca depósito bancario): Debe cuenta del motivo / Haber caja. Identidad = movimiento.
/// </summary>
public sealed class CashManualMovementPostingTranslator(
    AccountingPostingRecorder recorder,
    ICompanyClock companyClock,
    ICompanyPrecisionPolicyProvider precision)
    : INotificationHandler<CashManualMovementRecordedEvent>
{
    public const string SourceModuleName = CashPosting.SourceModuleName;

    public static string FactTypeFor(CashMovementType type) => type.ToString();

    public async Task Handle(CashManualMovementRecordedEvent e, CancellationToken ct)
    {
        var tenantId = e.TenantId!.Value;
        var decimals = (await precision.GetEffectiveAsync(ct)).AccountingDecimals;
        var amount = Math.Round(e.Amount, decimals, MidpointRounding.AwayFromZero);
        if (amount <= 0m)
            return;
        var entryDate = await companyClock.LocalDateAsync(e.CompanyId, tenantId, e.RecordedAtUtc, ct);
        var isIncome = e.MovementType == CashMovementType.ManualIncome;

        await recorder.RecordAsync(
            new AccountingPostingRequest(
                tenantId,
                e.CompanyId,
                SourceModuleName,
                FactTypeFor(e.MovementType),
                e.CashMovementId,
                null,
                entryDate,
                entryDate,
                CashPosting.Payload(
                    amount,
                    new AccountingPostingDestination(AccountingPostingDestination.CashRegister, e.CashRegisterId,
                        amount, isIncome ? AccountNature.Debit : AccountNature.Credit, Description: e.Description),
                    new AccountingPostingDestination(AccountingPostingDestination.CashMovementReason, e.ReasonId,
                        amount, isIncome ? AccountNature.Credit : AccountNature.Debit, Description: e.Description)
                ),
                e.CashSessionId
            ),
            ct
        );
    }
}

/// <summary>Depósito bancario: Debe cuenta del banco destino / Haber caja. Identidad = movimiento.</summary>
public sealed class CashBankDepositPostingTranslator(
    AccountingPostingRecorder recorder,
    ICompanyClock companyClock,
    ICompanyPrecisionPolicyProvider precision)
    : INotificationHandler<CashBankDepositRecordedEvent>
{
    public const string SourceModuleName = CashPosting.SourceModuleName;
    public const string FactTypeName = CashPosting.BankDepositFactType;

    public async Task Handle(CashBankDepositRecordedEvent e, CancellationToken ct)
    {
        var tenantId = e.TenantId!.Value;
        var decimals = (await precision.GetEffectiveAsync(ct)).AccountingDecimals;
        var amount = Math.Round(e.Amount, decimals, MidpointRounding.AwayFromZero);
        if (amount <= 0m)
            return;
        var entryDate = await companyClock.LocalDateAsync(e.CompanyId, tenantId, e.RecordedAtUtc, ct);

        await recorder.RecordAsync(
            new AccountingPostingRequest(
                tenantId,
                e.CompanyId,
                SourceModuleName,
                FactTypeName,
                e.CashMovementId,
                e.ReferenceNumber,
                entryDate,
                entryDate,
                CashPosting.Payload(
                    amount,
                    new AccountingPostingDestination(AccountingPostingDestination.BankAccount, e.CompanyBankAccountId,
                        amount, AccountNature.Debit, Description: e.ReferenceNumber),
                    new AccountingPostingDestination(AccountingPostingDestination.CashRegister, e.CashRegisterId,
                        amount, AccountNature.Credit, Description: e.ReferenceNumber)
                ),
                e.CashSessionId
            ),
            ct
        );
    }
}

/// <summary>
/// Diferencia de arqueo al cerrar: sobrante → Debe caja / Haber "Diferencias positivas de caja" (regla);
/// faltante → Debe "Descuadres de caja" (regla) / Haber caja. Importe = |Difference|. Sin diferencia el
/// dominio no publica el hecho. Identidad = sesión.
/// </summary>
public sealed class CashSessionDifferencePostingTranslator(
    AccountingPostingRecorder recorder,
    ICompanyClock companyClock,
    ICompanyPrecisionPolicyProvider precision)
    : INotificationHandler<CashSessionClosedWithDifferenceEvent>
{
    public const string SourceModuleName = CashPosting.SourceModuleName;

    public static string FactTypeFor(decimal difference) =>
        difference > 0m ? CashPosting.SurplusFactType : CashPosting.ShortageFactType;

    public async Task Handle(CashSessionClosedWithDifferenceEvent e, CancellationToken ct)
    {
        var tenantId = e.TenantId!.Value;
        var decimals = (await precision.GetEffectiveAsync(ct)).AccountingDecimals;
        var amount = Math.Round(Math.Abs(e.Difference), decimals, MidpointRounding.AwayFromZero);
        if (amount <= 0m)
            return;
        var entryDate = await companyClock.LocalDateAsync(e.CompanyId, tenantId, e.ClosedAtUtc, ct);
        var isSurplus = e.Difference > 0m;

        await recorder.RecordAsync(
            new AccountingPostingRequest(
                tenantId,
                e.CompanyId,
                SourceModuleName,
                FactTypeFor(e.Difference),
                e.CashSessionId,
                null,
                entryDate,
                entryDate,
                CashPosting.Payload(
                    amount,
                    new AccountingPostingDestination(AccountingPostingDestination.CashRegister, e.CashRegisterId,
                        amount, isSurplus ? AccountNature.Debit : AccountNature.Credit)
                ),
                e.CashSessionId
            ),
            ct
        );
    }
}
