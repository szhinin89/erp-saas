using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Auth.UseCases.SwitchCompany;

public sealed record SwitchCompanyCommand(Guid CompanyId) : IRequest<Result<AuthResponseDto>>;
