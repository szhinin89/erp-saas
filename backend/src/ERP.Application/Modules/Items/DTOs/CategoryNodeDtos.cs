namespace ERP.Application.Items.DTOs;

public sealed record CategoryNodeDto(
    Guid Id,
    Guid? ParentId,
    string Code,
    string Name,
    string? Description,
    string Level,
    string Path,
    int Depth,
    int SortOrder,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public sealed record CategoryTreeDto(IReadOnlyList<CategoryNodeDto> Nodes, int MaxDepth);
