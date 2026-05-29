using MediatR;
using ERP.Application.Common;
using ERP.Application.Configuration.DTOs;
using ERP.Domain.Configuration.Interfaces;

namespace ERP.Application.Configuration.UseCases.GetSriSettings;

public sealed class GetSriConfigurationQueryHandler
    : IRequestHandler<GetSriConfigurationQuery, Result<SriConfigurationDto?>>
{
    private readonly ISriSettingsRepository _repo;
    private readonly ICurrentCompany _currentCompany;

    public GetSriConfigurationQueryHandler(
        ISriSettingsRepository repo,
        ICurrentCompany currentCompany)
    {
        _repo           = repo;
        _currentCompany = currentCompany;
    }

    public async Task<Result<SriConfigurationDto?>> Handle(
        GetSriConfigurationQuery query, CancellationToken ct)
    {
        var config = await _repo.GetByCompanyIdAsync(_currentCompany.CompanyId, ct);
        if (config is null)
            return Result<SriConfigurationDto?>.Success(null);

        return Result<SriConfigurationDto?>.Success(new SriConfigurationDto(
            config.CompanyId,
            config.CertP12Path,
            config.Environment,
            config.EmissionType,
            config.WsdlUrl));
    }
}
