using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using ERP.Application.Platform.Auth.UseCases.PlatformLogin;
using MediatR;

namespace ERP.Application.Auth.UseCases.SuperAdminLogin;

/// <summary>Alias legacy — delega al handler canónico platform.</summary>
[Obsolete("Use POST /api/platform/auth/login via PlatformLoginCommand.")]
public class SuperAdminLoginHandler : IRequestHandler<SuperAdminLoginCommand, Result<AuthResponseDto>>
{
    private readonly IMediator _mediator;

    public SuperAdminLoginHandler(IMediator mediator) => _mediator = mediator;

    public Task<Result<AuthResponseDto>> Handle(SuperAdminLoginCommand command, CancellationToken ct)
        => _mediator.Send(new PlatformLoginCommand(command.Email, command.Password), ct);
}
