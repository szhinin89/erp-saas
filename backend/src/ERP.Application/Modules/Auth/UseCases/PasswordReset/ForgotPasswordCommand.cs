using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Auth.UseCases.PasswordReset;

public record ForgotPasswordCommand(string Email) : IRequest<Result<bool>>;
