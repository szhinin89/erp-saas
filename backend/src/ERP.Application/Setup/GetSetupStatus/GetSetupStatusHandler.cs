using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Setup.GetSetupStatus;

public sealed class GetSetupStatusHandler
    : IRequestHandler<GetSetupStatusQuery, Result<SetupStatusDto>>
{
    private readonly ISystemSetupRepository _repo;

    public GetSetupStatusHandler(ISystemSetupRepository repo) => _repo = repo;

    public async Task<Result<SetupStatusDto>> Handle(
        GetSetupStatusQuery request,
        CancellationToken cancellationToken
    )
    {
        var state = await _repo.GetAsync(cancellationToken);
        var dto = new SetupStatusDto(
            IsInitialized: state?.IsInitialized ?? false,
            AdminEmail: state?.IsInitialized == true ? state.AdminEmail : null
        );
        return Result<SetupStatusDto>.Success(dto);
    }
}
