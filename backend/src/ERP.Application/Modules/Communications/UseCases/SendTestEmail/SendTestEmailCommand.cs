using ERP.Application.Common;
using ERP.Application.Modules.Communications.DTOs;
using MediatR;

namespace ERP.Application.Modules.Communications.UseCases.SendTestEmail;

public sealed record SendTestEmailCommand(string ToEmail) : IRequest<Result<SendTestEmailResultDto>>;
