using ERP.Application.Common;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Modules.ElectronicInvoicing.DTOs;
using ERP.Application.Modules.ElectronicInvoicing.Enums;
using ERP.Application.Modules.ElectronicInvoicing.Health;
using ERP.Application.Modules.ElectronicInvoicing.Services;
using ERP.Domain.Configuration.Interfaces;
using MediatR;

namespace ERP.Application.Modules.ElectronicInvoicing.UseCases.GetSriServiceHealth;

public sealed class GetSriServiceHealthQueryHandler
    : IRequestHandler<GetSriServiceHealthQuery, Result<SriServiceHealthDto>>
{
    private readonly ISriSettingsRepository _repo;
    private readonly ICurrentCompany _currentCompany;
    private readonly ISriServiceHealthReader _reader;

    public GetSriServiceHealthQueryHandler(
        ISriSettingsRepository repo,
        ICurrentCompany currentCompany,
        ISriServiceHealthReader reader
    )
    {
        _repo = repo;
        _currentCompany = currentCompany;
        _reader = reader;
    }

    public async Task<Result<SriServiceHealthDto>> Handle(
        GetSriServiceHealthQuery request,
        CancellationToken cancellationToken
    )
    {
        var settings = await _repo.GetByCompanyIdAsync(_currentCompany.CompanyId, cancellationToken);
        if (settings is null || !SriWsdlUrl.IsValidFor(settings.WsdlUrl, settings.Environment))
        {
            var notConfigured = SriServiceHealthEvaluator.Unverified(SriServiceHealthReason.NotConfigured);
            var (env, envName) = settings is null
                ? ((string?)null, (string?)null)
                : SriEnvironmentDisplay.Resolve(settings.Environment);
            return Result<SriServiceHealthDto>.Success(
                new SriServiceHealthDto(env, envName, notConfigured, notConfigured)
            );
        }

        var (environment, environmentName) = SriEnvironmentDisplay.Resolve(settings.Environment);
        return Result<SriServiceHealthDto>.Success(
            new SriServiceHealthDto(
                environment,
                environmentName,
                _reader.GetStatus(settings.WsdlUrl, SriServiceKind.Reception),
                _reader.GetStatus(settings.WsdlUrl, SriServiceKind.Authorization)
            )
        );
    }
}
