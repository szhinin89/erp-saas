using ERP.Application.Common;
using ERP.Application.Modules.ElectronicInvoicing.DTOs;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using MediatR;

namespace ERP.Application.Modules.ElectronicInvoicing.UseCases.UpsertSystemProviderSettings;

public sealed class UpsertSystemProviderSettingsCommandHandler
    : IRequestHandler<UpsertSystemProviderSettingsCommand, Result<SystemProviderSettingsDto>>
{
    private readonly ISystemProviderSettingsRepository _repo;
    private readonly ICurrentUser _currentUser;

    public UpsertSystemProviderSettingsCommandHandler(
        ISystemProviderSettingsRepository repo,
        ICurrentUser currentUser
    )
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<Result<SystemProviderSettingsDto>> Handle(
        UpsertSystemProviderSettingsCommand command,
        CancellationToken cancellationToken
    )
    {
        var settings = await _repo.GetAsync(cancellationToken);
        var isNew = settings is null;
        settings ??= SystemProviderSettings.CreateNew();

        try
        {
            settings.Configure(
                command.Ruc,
                command.LegalName,
                command.CiiuCode,
                command.EffectiveDate,
                command.Enabled,
                _currentUser.UserId
            );
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result<SystemProviderSettingsDto>.ValidationFailure(ex.Message);
        }

        if (isNew)
            await _repo.AddAsync(settings, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);

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
