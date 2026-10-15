using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Access.UseCases.ExpireUserSessions;

/// <summary>System-wide passive expiration with an explicit bounded batch; returns expired count.</summary>
public sealed record ExpireUserSessionsCommand(int BatchSize) : IRequest<Result<int>>;
