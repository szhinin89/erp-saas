using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.ElectronicInvoicing.DTOs;
using ERP.Domain.Configuration.Interfaces;

namespace ERP.Application.Modules.ElectronicInvoicing.UseCases.GetSriConfiguration;

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
        GetSriConfigurationQuery query, CancellationToken cancellationToken)
    {
        var config = await _repo.GetByCompanyIdAsync(_currentCompany.CompanyId, cancellationToken);
        if (config is null)
            return Result<SriConfigurationDto?>.Success(null);

        return Result<SriConfigurationDto?>.Success(new SriConfigurationDto(
            config.CompanyId,
            HasCertificate: !string.IsNullOrWhiteSpace(config.CertP12Path),
            config.CertFileName,
            config.CertSizeBytes,
            config.CertUploadedAtUtc,
            config.Environment,
            config.EmissionType,
            config.WsdlUrl));
    }
}
