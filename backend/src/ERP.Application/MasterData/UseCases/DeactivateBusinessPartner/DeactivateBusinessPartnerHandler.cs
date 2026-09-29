using ERP.Application.Common;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.DeactivateBusinessPartner;

public sealed class DeactivateBusinessPartnerHandler
    : IRequestHandler<DeactivateBusinessPartnerCommand, Result<bool>>
{
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IOperationalContext _ctx;

    public DeactivateBusinessPartnerHandler(
        IBusinessPartnerRepository bpRepo,
        IOperationalContext ctx
    ) => (_bpRepo, _ctx) = (bpRepo, ctx);

    public async Task<Result<bool>> Handle(
        DeactivateBusinessPartnerCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var bp = await _bpRepo.GetByIdAsync(cmd.Id, cancellationToken);
        if (bp is null)
            return Result<bool>.NotFound("BusinessPartner no encontrado.");

        bp.Deactivate(_ctx.UserId);

        await _bpRepo.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
