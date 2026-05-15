using MediatR;
using ERP.Application.Common;
using ERP.Application.Configuration.DTOs;
using ERP.Domain.Configuration.Interfaces;

namespace ERP.Application.Configuration.UseCases.GetSriSettings;

public sealed class GetConfiguracionSRIQueryHandler
    : IRequestHandler<GetConfiguracionSRIQuery, Result<ConfiguracionSRIDto?>>
{
    private readonly ISriSettingsRepository _repo;
    private readonly ICurrentTenant              _currentTenant;

    public GetConfiguracionSRIQueryHandler(
        ISriSettingsRepository repo,
        ICurrentTenant currentTenant)
    {
        _repo          = repo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<ConfiguracionSRIDto?>> Handle(
        GetConfiguracionSRIQuery query, CancellationToken ct)
    {
        var config = await _repo.GetByTenantIdAsync(_currentTenant.TenantId, ct);
        if (config is null)
            return Result<ConfiguracionSRIDto?>.Success(null);

        return Result<ConfiguracionSRIDto?>.Success(new ConfiguracionSRIDto(
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
