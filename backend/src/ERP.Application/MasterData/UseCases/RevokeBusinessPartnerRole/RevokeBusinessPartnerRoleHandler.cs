using ERP.Application.Common;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.RevokeBusinessPartnerRole;

public sealed class RevokeBusinessPartnerRoleHandler
    : IRequestHandler<RevokeBusinessPartnerRoleCommand, Result<bool>>
{
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly IOperationalContext _ctx;

    public RevokeBusinessPartnerRoleHandler(
        IBusinessPartnerRoleRepository roleRepo,
        IOperationalContext ctx
    ) => (_roleRepo, _ctx) = (roleRepo, ctx);

    public async Task<Result<bool>> Handle(
        RevokeBusinessPartnerRoleCommand cmd,
        CancellationToken cancellationToken
    )
    {
        // TODO ADR-BP-14: verificar documentos activos (compras/ventas) antes de revocar
        // cuando el módulo de documentos esté implementado.

        var role = await _roleRepo.GetByIdAsync(cmd.RoleId, cancellationToken);
        if (role is null || role.BusinessPartnerId != cmd.BusinessPartnerId)
            return Result<bool>.NotFound("Rol no encontrado.");

        role.Revoke(_ctx.UserId);

        await _roleRepo.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
