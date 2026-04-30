namespace ERP.Application.Access.UseCases.Profiles;

public record CreateProfileCommand(string Name, string? Description);
public record UpdateProfileCommand(Guid ProfileId, string Name, string? Description, bool IsActive);

