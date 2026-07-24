using ERP.Application.Common;
using ERP.Application.Common.Config;
using ERP.Domain.Access.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.ExpireUserSessions;

/// <summary>
/// Idempotente por construcción: GetExpiredActiveSessionsAsync solo devuelve sesiones con
/// Status == Active, así que una sesión ya marcada Expired en una corrida anterior nunca
/// vuelve a aparecer en corridas posteriores — no hay riesgo de doble expiración ni de
/// sobrescribir ClosedAt.
/// </summary>
public sealed class ExpireUserSessionsHandler : IRequestHandler<ExpireUserSessionsCommand, Result<int>>
{
    private static readonly Guid SystemActor = Guid.Empty;

    private readonly IUserSessionRepository _repository;
    private readonly SessionExpirationOptions _options;

    public ExpireUserSessionsHandler(IUserSessionRepository repository, SessionExpirationOptions options)
    {
        _repository = repository;
        _options = options;
    }

    public async Task<Result<int>> Handle(ExpireUserSessionsCommand request, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-_options.MaxSessionAgeDays);
        var candidates = await _repository.GetExpiredActiveSessionsAsync(cutoff, cancellationToken);

        if (candidates.Count == 0)
            return Result<int>.Success(0);

        foreach (var session in candidates)
        {
            session.MarkExpired(SystemActor);
            await _repository.UpdateAsync(session, cancellationToken);
        }

        await _repository.SaveChangesAsync(cancellationToken);

        return Result<int>.Success(candidates.Count);
    }
}
