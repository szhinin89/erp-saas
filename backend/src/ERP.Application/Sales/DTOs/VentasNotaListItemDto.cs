namespace ERP.Application.Sales.DTOs;

public record SalesNoteListItemDto(
    Guid Id,
    Guid OriginalInvoiceId,
    string NoteType,
    string    Status,
    string    AccessKey,
    decimal Total,
    DateTime  IssueDate);
