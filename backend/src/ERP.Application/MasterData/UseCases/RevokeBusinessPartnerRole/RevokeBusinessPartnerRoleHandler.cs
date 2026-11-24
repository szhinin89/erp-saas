using ERP.Application.Common;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.RevokeBusinessPartnerRole;

public sealed class RevokeBusinessPartnerRoleHandler
    : IRequestHandler<RevokeBusinessPartnerRoleCommand, Result<bool>>
{
    private readonly IBusinessPartnerRoleRepository _roleRepo;
    private readonly IOperationalContext _ctx;
    private readonly IBusinessPartnerEmploymentGuard _employmentGuard;
    private readonly IUnitOfWork _unitOfWork;

    public RevokeBusinessPartnerRoleHandler(
        IBusinessPartnerRoleRepository roleRepo,
        IOperationalContext ctx,
        IBusinessPartnerEmploymentGuard employmentGuard,
        IUnitOfWork unitOfWork
    ) => (_roleRepo, _ctx, _employmentGuard, _unitOfWork) = (roleRepo, ctx, employmentGuard, unitOfWork);

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

        // Solo el rol Employee está protegido (EMP-T-03): el resto conserva el comportamiento previo.
        if (role.RoleType != RoleType.Employee)
        {
            role.Revoke(_ctx.UserId);

            await _roleRepo.SaveChangesAsync(cancellationToken);
            return Result<bool>.Success(true);
        }

        // EMP-E3 (INV-10): protocolo lock → guardia → escritura en una misma transacción; el tenant
        // sale del contexto autenticado.
        var ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            await _employmentGuard.AcquireBpEmploymentLockAsync(
                _ctx.TenantId,
                role.BusinessPartnerId,
                cancellationToken
            );
            if (
                await _employmentGuard.HasActiveEmploymentAsync(
                    _ctx.TenantId,
                    role.BusinessPartnerId,
                    cancellationToken
                )
            )
            {
                if (ownsTransaction)
                    await _unitOfWork.RollbackAsync(cancellationToken);
                return Result<bool>.Failure(
                    "No se puede revocar el rol de Empleado mientras el tercero tenga una ficha laboral activa.",
                    ApiResponseCodes.MasterData.BpHasActiveEmployment
                );
            }

            role.Revoke(_ctx.UserId);

            await _roleRepo.SaveChangesAsync(cancellationToken);
            if (ownsTransaction)
                await _unitOfWork.CommitAsync(cancellationToken);
            return Result<bool>.Success(true);
        }
        catch
        {
            if (ownsTransaction)
                await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
