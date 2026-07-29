using ERP.Application.Common;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.MasterData.Interfaces;
using MediatR;

namespace ERP.Application.MasterData.UseCases.BlockBusinessPartner;

public sealed class BlockBusinessPartnerHandler
    : IRequestHandler<BlockBusinessPartnerCommand, Result<bool>>
{
    private readonly ICompanyBpTradingSettingsRepository _settingsRepo;
    private readonly IUserActivityRepository _activity;
    private readonly IOperationalContext _ctx;
    private readonly ICurrentUser _currentUser;

    public BlockBusinessPartnerHandler(
        ICompanyBpTradingSettingsRepository settingsRepo,
        IUserActivityRepository activity,
        IOperationalContext ctx,
        ICurrentUser currentUser
    )
    {
        _settingsRepo = settingsRepo;
        _activity = activity;
        _ctx = ctx;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(
        BlockBusinessPartnerCommand cmd,
        CancellationToken cancellationToken
    )
    {
        var settings = await _settingsRepo.GetByBusinessPartnerAsync(
            cmd.BusinessPartnerId,
            cancellationToken
        );
        if (settings is null)
            return Result<bool>.NotFound(
                "No existe configuración comercial para este BP en la empresa activa. Cree la configuración primero."
            );

        try
        {
            settings.Block(cmd.Reason, _ctx.UserId);
        }
        catch (ArgumentException ex)
        {
            return Result<bool>.ValidationFailure(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.ValidationFailure(ex.Message);
        }

        await _activity.AddAsync(
            UserActivity.Create(
                _ctx.TenantId,
                _ctx.UserId,
                _currentUser.Email,
                _currentUser.FullName,
                module: "masterdata",
                action: "business-partner.block",
                entityType: "BusinessPartner",
                entityId: cmd.BusinessPartnerId,
                description: $"BP bloqueado. Motivo: {cmd.Reason}"
            ),
            cancellationToken
        );

        await _settingsRepo.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
