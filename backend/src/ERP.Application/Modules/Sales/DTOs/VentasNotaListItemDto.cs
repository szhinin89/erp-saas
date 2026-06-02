using ERP.Domain.Common;

namespace ERP.Application.Sales.DTOs;

public record SalesNoteListItemDto(
    Guid     Id,
    Guid     OriginalInvoiceId,
    NoteType NoteType,
    string   Status,
    string   AccessKey,
    decimal  Total,
    DateTime IssueDate);
