using ERP.Domain.Common;
namespace ERP.Application.Modules.Accounting.DTOs;

public record JournalEntryDto(
    Guid Id,
    string Reference,
    DateTime Date,
    string Description,
    DocumentStatus Status,
    IReadOnlyList<JournalEntryLineDto> Lines,
    DateTime CreatedAt
);

public record JournalEntryLineDto(
    Guid Id,
    Guid AccountId,
    decimal DebitAmount,
    string DebitCurrency,
    decimal CreditAmount,
    string CreditCurrency
);
