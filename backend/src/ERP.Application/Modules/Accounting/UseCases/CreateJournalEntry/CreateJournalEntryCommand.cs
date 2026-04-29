using ERP.Application.Accounting.UseCases.CreateJournalEntry;
namespace ERP.Application.Accounting.UseCases.CreateJournalEntry;

public record CreateJournalEntryCommand(
    string Reference,
    DateTime Date,
    string Description,
    IReadOnlyList<JournalEntryLineCommand> Lines
);

public record JournalEntryLineCommand(
    Guid AccountId,
    decimal DebitAmount,
    decimal CreditAmount,
    string Currency
);
