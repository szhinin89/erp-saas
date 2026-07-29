using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Auth.UseCases.SwitchCompany;

/// <summary>Ver nota de TerminalId en <see cref="ERP.Application.Auth.UseCases.Login.LoginCommand"/>.</summary>
public sealed record SwitchCompanyCommand(Guid CompanyId, string? TerminalId = null)
    : IRequest<Result<AuthResponseDto>>;
