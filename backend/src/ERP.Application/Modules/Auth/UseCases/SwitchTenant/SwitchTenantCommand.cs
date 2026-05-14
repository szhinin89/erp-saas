using MediatR;
using ERP.Application.Common;
using ERP.Application.Auth.DTOs;

namespace ERP.Application.Auth.UseCases.SwitchTenant;

public record SwitchTenantCommand(Guid TenantId) : IRequest<Result<AuthResponseDto>>;
