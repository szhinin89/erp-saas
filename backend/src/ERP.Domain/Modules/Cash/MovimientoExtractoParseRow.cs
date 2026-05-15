namespace ERP.Domain.Modules.Cash;

public sealed record StatementParseRow(
    DateTime TransactionDate,
    string   Description,
    decimal  Amount,
    string   TransactionType,
    string?  Reference);
