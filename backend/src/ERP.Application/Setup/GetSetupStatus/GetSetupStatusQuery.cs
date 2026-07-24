using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Setup.GetSetupStatus;

public sealed record GetSetupStatusQuery : IRequest<Result<SetupStatusDto>>;

public sealed record SetupStatusDto(bool IsInitialized, string? AdminEmail);
