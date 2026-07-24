using MediatR;
using ERP.Application.Common;
using ERP.Application.Auth.DTOs;

namespace ERP.Application.Auth.UseCases.RefreshToken;

public record RefreshTokenCommand(string RawRefreshToken) : IRequest<Result<AuthResponseDto>>;
