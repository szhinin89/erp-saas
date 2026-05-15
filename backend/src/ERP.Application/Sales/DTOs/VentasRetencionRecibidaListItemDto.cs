namespace ERP.Application.Sales.DTOs;

public sealed record SalesRetentionListItemDto(
    Guid Id,
    Guid      CustomerId,
    string    AccessKey,
    DateTime  IssueDate,
    decimal ValorRetenido,
    Guid? SalesBillId);
