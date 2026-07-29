using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Auth.UseCases.Logout;

public record LogoutCommand(string RawRefreshToken, bool AllDevices) : IRequest<Result<string>>;
