using ERP.Application.Common;

namespace ERP.Application.Common.Interfaces;

public interface IAccountingService
{
    Task<Result<Guid>> CreatePurchaseJournalEntryAsync(
        Guid     purchBillId,
        string   reference,
        DateTime entryDate,
        decimal  subtotal,
        decimal  vatTotal,
        decimal  total,
        string   description,
        CancellationToken cancellationToken);

    Task<Result<Guid>> CreateSalesJournalEntryAsync(
        Guid     salesBillId,
        string   reference,
        DateTime entryDate,
        decimal  subtotal,
        decimal  vatTotal,
        decimal  total,
        string   description,
        CancellationToken cancellationToken);

    Task<Result<Guid>> CreateExpenseJournalEntryAsync(
        Guid     expenseId,
        string   category,
        string   reference,
        DateTime entryDate,
        decimal  subtotal,
        decimal  vatTotal,
        decimal  total,
        string   description,
        CancellationToken cancellationToken);

    Task<Result<Guid>> CreateSalesCreditNoteJournalEntryAsync(
        Guid     noteId,
        string   reference,
        DateTime entryDate,
        decimal  subtotal,
        decimal  vatTotal,
        decimal  total,
        string   description,
        CancellationToken cancellationToken);

    Task<Result<Guid>> CreateSalesDebitNoteJournalEntryAsync(
        Guid     noteId,
        string   reference,
        DateTime entryDate,
        decimal  subtotal,
        decimal  vatTotal,
        decimal  total,
        string   description,
        CancellationToken cancellationToken);

    Task<Result<Guid>> CreateIssuedWithholdingJournalEntryAsync(
        Guid     retentionId,
        string   reference,
        DateTime entryDate,
        decimal  totalRetained,
        string   description,
        CancellationToken cancellationToken);

    Task<Result<Guid>> CreateReceivedWithholdingJournalEntryAsync(
        Guid     retentionId,
        string   reference,
        DateTime entryDate,
        decimal  totalRetained,
        string   description,
        CancellationToken cancellationToken);

    Task<Result<Guid>> CreatePurchaseSupplierCreditNoteJournalEntryAsync(
        Guid     noteId,
        string   reference,
        DateTime entryDate,
        decimal  subtotal,
        decimal  vatTotal,
        decimal  total,
        string   description,
        CancellationToken cancellationToken);

    Task<Result<Guid>> CreatePurchaseSupplierDebitNoteJournalEntryAsync(
        Guid     noteId,
        string   reference,
        DateTime entryDate,
        decimal  subtotal,
        decimal  vatTotal,
        decimal  total,
        string   description,
        CancellationToken cancellationToken);

    Task<Result<Guid>> CreateExpenseSupplierCreditNoteJournalEntryAsync(
        Guid     noteId,
        string   reference,
        DateTime entryDate,
        decimal  total,
        string   category,
        string   description,
        CancellationToken cancellationToken);

    Task<Result<Guid>> CreateExpenseSupplierDebitNoteJournalEntryAsync(
        Guid     noteId,
        string   reference,
        DateTime entryDate,
        decimal  total,
        string   category,
        string   description,
        CancellationToken cancellationToken);
}
