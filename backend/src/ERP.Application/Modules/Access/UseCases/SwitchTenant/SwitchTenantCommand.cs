using MediatR;
using ERP.Application.Common;
using ERP.Application.Access.DTOs;

namespace ERP.Application.Access.UseCases.SwitchTenant;

public record SwitchTenantCommand(
    Guid TenantId
) : IRequest<Result<SessionResponseDto>>;

