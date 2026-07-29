using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Auth.UseCases.PasswordReset;

public record ForgotPasswordCommand(string Email) : IRequest<Result<bool>>;
