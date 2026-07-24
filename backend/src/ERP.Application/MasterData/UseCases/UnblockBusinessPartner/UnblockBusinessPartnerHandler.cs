using ERP.Application.Common;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.UnblockBusinessPartner;

public sealed class UnblockBusinessPartnerHandler
    : IRequestHandler<UnblockBusinessPartnerCommand, Result<bool>>
{
    private readonly ICompanyBpTradingSettingsRepository _settingsRepo;
    private readonly IOperationalContext                 _ctx;

    public UnblockBusinessPartnerHandler(
        ICompanyBpTradingSettingsRepository settingsRepo, IOperationalContext ctx)
        => (_settingsRepo, _ctx) = (settingsRepo, ctx);

    public async Task<Result<bool>> Handle(UnblockBusinessPartnerCommand cmd, CancellationToken cancellationToken)
    {
        var settings = await _settingsRepo.GetByBusinessPartnerAsync(cmd.BusinessPartnerId, cancellationToken);
        if (settings is null)
            return Result<bool>.NotFound("No existe configuración comercial para este BP en la empresa activa.");

        try { settings.Unblock(_ctx.UserId); }
        catch (InvalidOperationException ex) { return Result<bool>.ValidationFailure(ex.Message); }

        await _settingsRepo.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
