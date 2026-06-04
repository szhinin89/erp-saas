using ERP.Application.Common;
using MediatR;

namespace ERP.Application.MasterData.UseCases.RevokeBusinessPartnerRole;

/// <summary>
/// Revoca un rol. El registro se conserva para auditoría histórica.
/// ATENCIÓN: cuando el módulo de documentos exista, el handler debe verificar
/// documentos activos en este rol antes de revocar. Ver ADR-BP-14.
/// </summary>
public sealed record RevokeBusinessPartnerRoleCommand(Guid RoleId)
    : IRequest<Result<bool>>, ISubscriberScopedRequest;
