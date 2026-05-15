namespace ERP.Application.Modules.Purchasing.DTOs;

public sealed record IssuedRetentionListItemDto(
    Guid Id,
    Guid    SupplierId,
    string    AccessKey,
    string    Status,
    decimal TotalRetenido,
    DateTime  IssueDate);
