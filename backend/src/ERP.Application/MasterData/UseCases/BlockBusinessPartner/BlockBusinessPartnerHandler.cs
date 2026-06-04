using ERP.Application.Common;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.BlockBusinessPartner;

public sealed class BlockBusinessPartnerHandler
    : IRequestHandler<BlockBusinessPartnerCommand, Result<bool>>
{
    private readonly ICompanyBpTradingSettingsRepository _settingsRepo;
    private readonly IOperationalContext                 _ctx;

    public BlockBusinessPartnerHandler(
        ICompanyBpTradingSettingsRepository settingsRepo, IOperationalContext ctx)
        => (_settingsRepo, _ctx) = (settingsRepo, ctx);

    public async Task<Result<bool>> Handle(BlockBusinessPartnerCommand cmd, CancellationToken ct)
    {
        var settings = await _settingsRepo.GetByBusinessPartnerAsync(cmd.BusinessPartnerId, ct);
        if (settings is null)
            return Result<bool>.NotFound(
                "No existe configuración comercial para este BP en la empresa activa. Cree la configuración primero.");

        try { settings.Block(cmd.Reason, _ctx.UserId); }
        catch (ArgumentException ex)        { return Result<bool>.ValidationFailure(ex.Message); }
        catch (InvalidOperationException ex) { return Result<bool>.ValidationFailure(ex.Message); }

        await _settingsRepo.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
