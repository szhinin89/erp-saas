using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Access.UseCases.Profiles;

// SEC-05: los perfiles son datos COMPANY — IRequiresCompanyContext exige empresa activa con
// membership (o operador autorizado) vía CompanyScopeBehavior antes del handler.

public record CreateProfileCommand(string Name, string? Description)
    : IRequest<Result<ProfileDto>>,
        IRequiresCompanyContext;

public record UpdateProfileCommand(Guid ProfileId, string Name, string? Description, bool IsActive)
    : IRequest<Result<ProfileDto>>,
        IRequiresCompanyContext;

public record GetProfilesQuery(bool OnlyActive)
    : IRequest<Result<IReadOnlyList<ProfileDto>>>,
        IRequiresCompanyContext;
