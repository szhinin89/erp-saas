namespace ERP.Application.Access.UseCases.Profiles;

public record ProfileDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive
);

