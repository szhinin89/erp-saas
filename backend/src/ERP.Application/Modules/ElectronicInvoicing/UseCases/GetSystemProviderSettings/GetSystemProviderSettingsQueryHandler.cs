using ERP.Application.Common;
using ERP.Application.Modules.ElectronicInvoicing.DTOs;
using ERP.Domain.Configuration.Interfaces;
using MediatR;

namespace ERP.Application.Modules.ElectronicInvoicing.UseCases.GetSystemProviderSettings;

public sealed class GetSystemProviderSettingsQueryHandler
    : IRequestHandler<GetSystemProviderSettingsQuery, Result<SystemProviderSettingsDto>>
{
    private readonly ISystemProviderSettingsRepository _repo;

    public GetSystemProviderSettingsQueryHandler(ISystemProviderSettingsRepository repo) =>
        _repo = repo;

    public async Task<Result<SystemProviderSettingsDto>> Handle(
        GetSystemProviderSettingsQuery query,
        CancellationToken cancellationToken
    )
    {
        var settings = await _repo.GetAsync(cancellationToken);
        if (settings is null)
            return Result<SystemProviderSettingsDto>.Success(
                new SystemProviderSettingsDto(null, null, null, false, null, false, null)
            );

        return Result<SystemProviderSettingsDto>.Success(
            new SystemProviderSettingsDto(
                settings.Ruc,
                settings.LegalName,
                settings.CiiuCode,
                settings.Enabled,
                settings.EffectiveDate,
                settings.IsFullyConfigured,
                settings.UpdatedAtUtc
            )
        );
    }
}
