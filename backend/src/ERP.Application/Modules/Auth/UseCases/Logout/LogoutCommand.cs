using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Auth.UseCases.Logout;

public record LogoutCommand(string RawRefreshToken, bool AllDevices) : IRequest<Result<string>>;
