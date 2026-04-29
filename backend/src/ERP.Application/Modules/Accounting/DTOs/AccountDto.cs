namespace ERP.Application.Accounting.DTOs;

public record AccountDto(
    Guid Id,
    string Code,
    string Name,
    string Type,
    string Nature,
    bool IsActive,
    Guid? ParentId,
    DateTime CreatedAt
);
