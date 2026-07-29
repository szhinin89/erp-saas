using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Auth.UseCases.RefreshToken;

public record RefreshTokenCommand(string RawRefreshToken) : IRequest<Result<AuthResponseDto>>;
