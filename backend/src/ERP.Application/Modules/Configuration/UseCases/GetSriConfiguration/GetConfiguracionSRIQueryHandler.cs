using MediatR;
using ERP.Application.Common;
using ERP.Application.Configuration.DTOs;
using ERP.Domain.Configuration.Interfaces;

namespace ERP.Application.Configuration.UseCases.GetSriSettings;

public sealed class GetSriConfigurationQueryHandler
    : IRequestHandler<GetSriConfigurationQuery, Result<SriConfigurationDto?>>
{
    private readonly ISriSettingsRepository _repo;
    private readonly ICurrentSubscriber              _currentSubscriber;

    public GetSriConfigurationQueryHandler(
        ISriSettingsRepository repo,
        ICurrentSubscriber currentSubscriber)
    {
        _repo          = repo;
        _currentSubscriber = currentSubscriber;
    }

    public async Task<Result<SriConfigurationDto?>> Handle(
        GetSriConfigurationQuery query, CancellationToken ct)
    {
        var config = await _repo.GetBySubscriberIdAsync(_currentSubscriber.SubscriberId, ct);
        if (config is null)
            return Result<SriConfigurationDto?>.Success(null);

        return Result<SriConfigurationDto?>.Success(new SriConfigurationDto(
            config.SubscriberId,
            config.Ruc,
            config.LegalName,
            config.TradeName,
            config.MainAddress,
            config.RequiresAccounting,
            config.SpecialTaxpayer,
            config.EstabCode,
            config.EmPointCode,
            config.CurrentSequential,
            config.CertP12Path,
            config.Environment,
            config.EmissionType,
            config.WsdlUrl));
    }
}
