using MediatR;
using ERP.Application.Common;
using ERP.Application.Configuration.DTOs;
using ERP.Domain.Configuration.Interfaces;

namespace ERP.Application.Configuration.UseCases.GetSriSettings;

public sealed class GetSriConfigurationQueryHandler
    : IRequestHandler<GetSriConfigurationQuery, Result<SriConfigurationDto?>>
{
    private readonly ISriSettingsRepository _repo;
    private readonly ICurrentTenant              _currentTenant;

    public GetSriConfigurationQueryHandler(
        ISriSettingsRepository repo,
        ICurrentTenant currentTenant)
    {
        _repo          = repo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<SriConfigurationDto?>> Handle(
        GetSriConfigurationQuery query, CancellationToken ct)
    {
        var config = await _repo.GetByTenantIdAsync(_currentTenant.TenantId, ct);
        if (config is null)
            return Result<SriConfigurationDto?>.Success(null);

        return Result<SriConfigurationDto?>.Success(new SriConfigurationDto(
            config.TenantId,
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
