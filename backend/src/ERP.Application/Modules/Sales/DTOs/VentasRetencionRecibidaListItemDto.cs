namespace ERP.Application.Sales.DTOs;

public sealed record SalesRetentionListItemDto(
    Guid Id,
    Guid      CustomerId,
    string    AccessKey,
    DateTime  IssueDate,
    decimal RetainedAmount,
    Guid? SalesBillId);
