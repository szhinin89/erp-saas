using ERP.Application.Common;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.DeactivateBusinessPartner;

public sealed class DeactivateBusinessPartnerHandler
    : IRequestHandler<DeactivateBusinessPartnerCommand, Result<bool>>
{
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IOperationalContext _ctx;
    private readonly IBusinessPartnerEmploymentGuard _employmentGuard;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateBusinessPartnerHandler(
        IBusinessPartnerRepository bpRepo,
        IOperationalContext ctx,
        IBusinessPartnerEmploymentGuard employmentGuard,
        IUnitOfWork unitOfWork
    ) => (_bpRepo, _ctx, _employmentGuard, _unitOfWork) = (bpRepo, ctx, employmentGuard, unitOfWork);

    public async Task<Result<bool>> Handle(
        DeactivateBusinessPartnerCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var bp = await _bpRepo.GetByIdAsync(cmd.Id, cancellationToken);
        if (bp is null)
            return Result<bool>.NotFound("BusinessPartner no encontrado.");

        // EMP-E3 (EMP-T-03, INV-10): protocolo lock → guardia → escritura en una misma transacción.
        // El tenant sale del contexto autenticado, nunca del comando. Usa la transacción ambiente si
        // existe; si no, abre y confirma la suya.
        var ownsTransaction = !_unitOfWork.HasActiveTransaction;
        if (ownsTransaction)
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            await _employmentGuard.AcquireBpEmploymentLockAsync(
                _ctx.TenantId,
                bp.Id,
                cancellationToken
            );
            if (
                await _employmentGuard.HasActiveEmploymentAsync(
                    _ctx.TenantId,
                    bp.Id,
                    cancellationToken
                )
            )
            {
                if (ownsTransaction)
                    await _unitOfWork.RollbackAsync(cancellationToken);
                return Result<bool>.Failure(
                    "No se puede desactivar el tercero mientras tenga una ficha laboral activa.",
                    ApiResponseCodes.MasterData.BpHasActiveEmployment
                );
            }

            bp.Deactivate(_ctx.UserId);

            await _bpRepo.SaveChangesAsync(cancellationToken);
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
