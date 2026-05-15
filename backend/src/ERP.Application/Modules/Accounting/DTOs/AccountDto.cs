namespace ERP.Application.Modules.Accounting.DTOs;

public record AccountDto(
    Guid Id,
    string Code,
    string Name,
    string Type,
    string Nature,
    bool IsActive,
    bool AllowsMovements,
    Guid? ParentId,
    DateTime CreatedAt
);
