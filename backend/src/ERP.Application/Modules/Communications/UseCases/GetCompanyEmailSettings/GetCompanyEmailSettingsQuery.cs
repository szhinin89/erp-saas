using ERP.Application.Common;
using ERP.Application.Modules.Communications.DTOs;
using MediatR;

namespace ERP.Application.Modules.Communications.UseCases.GetCompanyEmailSettings;

public sealed record GetCompanyEmailSettingsQuery : IRequest<Result<CommunicationEmailSettingsDto>>;
