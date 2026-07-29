using ERP.Application.Common;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.ActivateBusinessPartner;

public sealed class ActivateBusinessPartnerHandler
    : IRequestHandler<ActivateBusinessPartnerCommand, Result<bool>>
{
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IOperationalContext _ctx;

    public ActivateBusinessPartnerHandler(
        IBusinessPartnerRepository bpRepo,
        IOperationalContext ctx
    ) => (_bpRepo, _ctx) = (bpRepo, ctx);

    public async Task<Result<bool>> Handle(
        ActivateBusinessPartnerCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var bp = await _bpRepo.GetByIdAsync(cmd.Id, cancellationToken);
        if (bp is null)
            return Result<bool>.NotFound("BusinessPartner no encontrado.");

        try
        {
            bp.Activate(_ctx.UserId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.ValidationFailure(ex.Message);
        }

        await _bpRepo.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
