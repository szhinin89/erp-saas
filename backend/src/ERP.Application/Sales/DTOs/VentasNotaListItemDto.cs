namespace ERP.Application.Sales.DTOs;

public record SalesNoteListItemDto(
    Guid Id,
    Guid FacturaOriginalId,
    string TipoNota,
    string    Status,
    string    AccessKey,
    decimal Total,
    DateTime  IssueDate);
