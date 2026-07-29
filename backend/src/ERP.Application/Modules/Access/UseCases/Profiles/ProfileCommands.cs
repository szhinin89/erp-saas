using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Access.UseCases.Profiles;

public record CreateProfileCommand(string Name, string? Description) : IRequest<Result<ProfileDto>>;

public record UpdateProfileCommand(Guid ProfileId, string Name, string? Description, bool IsActive)
    : IRequest<Result<ProfileDto>>;

public record GetProfilesQuery(bool OnlyActive) : IRequest<Result<IReadOnlyList<ProfileDto>>>;
