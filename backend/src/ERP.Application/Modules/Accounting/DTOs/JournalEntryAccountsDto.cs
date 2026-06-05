namespace ERP.Application.Modules.Accounting.DTOs;

/// <summary>Cuentas resueltas para armar un asiento (compra o venta).</summary>
public sealed record JournalEntryAccounts(
    Guid MainDebitAccountId,
    Guid MainCreditAccountId,
    Guid? VatDebitAccountId,
    Guid? VatCreditAccountId);
