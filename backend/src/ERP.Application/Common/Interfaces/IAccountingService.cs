using ERP.Application.Common;

namespace ERP.Application.Common.Interfaces;

public interface IAccountingService
{
    Task<Result<Guid>> CreatePurchaseJournalEntryAsync(
        Guid     purchBillId,
        string   reference,
        DateTime date,
        decimal  subtotal,
        decimal  vatTotal,
        decimal  total,
        string   description,
        CancellationToken ct);

    Task<Result<Guid>> CreateSalesJournalEntryAsync(
        Guid     salesBillId,
        string   reference,
        DateTime date,
        decimal  subtotal,
        decimal  vatTotal,
        decimal  total,
        string   description,
        CancellationToken ct);

    Task<Result<Guid>> CreateExpenseJournalEntryAsync(
        Guid     expenseId,
        string   category,
        string   reference,
        DateTime date,
        decimal  subtotal,
        decimal  vatTotal,
        decimal  total,
        string   description,
        CancellationToken ct);

    Task<Result<Guid>> CreateSalesCreditNoteJournalEntryAsync(
        Guid     noteId,
        string   reference,
        DateTime date,
        decimal  subtotal,
        decimal  vatTotal,
        decimal  total,
        string   description,
        CancellationToken ct);

    Task<Result<Guid>> CreateSalesDebitNoteJournalEntryAsync(
        Guid     noteId,
        string   reference,
        DateTime date,
        decimal  subtotal,
        decimal  vatTotal,
        decimal  total,
        string   description,
        CancellationToken ct);

    Task<Result<Guid>> CreateIssuedWithholdingJournalEntryAsync(
        Guid     retentionId,
        string   reference,
        DateTime date,
        decimal  totalRetained,
        string   description,
        CancellationToken ct);

    Task<Result<Guid>> CreateReceivedWithholdingJournalEntryAsync(
        Guid     retentionId,
        string   reference,
        DateTime date,
        decimal  totalRetained,
        string   description,
        CancellationToken ct);

    Task<Result<Guid>> CreatePurchaseSupplierCreditNoteJournalEntryAsync(
        Guid     noteId,
        string   reference,
        DateTime date,
        decimal  subtotal,
        decimal  vatTotal,
        decimal  total,
        string   description,
        CancellationToken ct);

    Task<Result<Guid>> CreatePurchaseSupplierDebitNoteJournalEntryAsync(
        Guid     noteId,
        string   reference,
        DateTime date,
        decimal  subtotal,
        decimal  vatTotal,
        decimal  total,
        string   description,
        CancellationToken ct);

    Task<Result<Guid>> CreateExpenseSupplierCreditNoteJournalEntryAsync(
        Guid     noteId,
        string   reference,
        DateTime date,
        decimal  total,
        string   category,
        string   description,
        CancellationToken ct);

    Task<Result<Guid>> CreateExpenseSupplierDebitNoteJournalEntryAsync(
        Guid     noteId,
        string   reference,
        DateTime date,
        decimal  total,
        string   category,
        string   description,
        CancellationToken ct);
}
