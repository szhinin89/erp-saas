using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.ElectronicInvoicing.DTOs;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using MediatR;

namespace ERP.Application.Modules.ElectronicInvoicing.UseCases.UpsertSriConfiguration;

public sealed class UpsertSriConfigurationCommandHandler
    : IRequestHandler<UpsertSriConfigurationCommand, Result<SriConfigurationDto>>
{
    private readonly ISriSettingsRepository _repo;
    private readonly IConfigurationChangeLogger _changeLogger;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _currentUser;
    private readonly ISecretProtector _secretProtector;

    public UpsertSriConfigurationCommandHandler(
        ISriSettingsRepository repo,
        IConfigurationChangeLogger changeLogger,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser,
        ISecretProtector secretProtector
    )
    {
        _repo = repo;
        _changeLogger = changeLogger;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _currentUser = currentUser;
        _secretProtector = secretProtector;
    }

    public async Task<Result<SriConfigurationDto>> Handle(
        UpsertSriConfigurationCommand command,
        CancellationToken cancellationToken
    )
    {
        var tenantId = _currentTenant.TenantId;
        var companyId = _currentCompany.CompanyId;
        var userId = _currentUser.UserId;
        var hasNewPassword = !string.IsNullOrWhiteSpace(command.CertPassword);

        var existing = await _repo.GetByCompanyIdAsync(companyId, cancellationToken);

        if (existing is null)
        {
            var config = SriSettings.Create(
                tenantId: tenantId,
                companyId: companyId,
                environment: command.Environment,
                emissionType: command.EmissionType,
                wsdlUrl: command.WsdlUrl,
                createdBy: userId
            );

            if (hasNewPassword)
            {
                config.UpdateConfiguration(
                    environment: command.Environment,
                    emissionType: command.EmissionType,
                    wsdlUrl: command.WsdlUrl,
                    certPassword: _secretProtector.Protect(command.CertPassword!.Trim()),
                    updatedBy: userId
                );
            }

            await _repo.AddAsync(config, cancellationToken);

            // CONFIG-FOUNDATION-P2-01: primera configuración — se audita igual que un cambio
            // (null → valor), nunca la contraseña en claro (ValueType.Masked).
            await LogFieldAsync(config, "Environment", null, command.Environment.ToString(), ConfigurationChangeValueType.Int, cancellationToken);
            await LogFieldAsync(config, "EmissionType", null, command.EmissionType.ToString(), ConfigurationChangeValueType.Int, cancellationToken);
            await LogFieldAsync(config, "WsdlUrl", null, command.WsdlUrl, ConfigurationChangeValueType.String, cancellationToken);
            if (hasNewPassword)
                await LogFieldAsync(config, "CertPassword", null, "***", ConfigurationChangeValueType.Masked, cancellationToken, isSensitive: true);

            await _repo.SaveChangesAsync(cancellationToken);
            return Result<SriConfigurationDto>.Success(ToDto(config));
        }

        // Contraseña vacía en una actualización = conservar la ya cifrada; solo se
        // re-encripta si el usuario efectivamente ingresó un valor nuevo.
        var certPasswordToPersist = hasNewPassword
            ? _secretProtector.Protect(command.CertPassword!.Trim())
            : null;

        var oldEnvironment = existing.Environment;
        var oldEmissionType = existing.EmissionType;
        var oldWsdlUrl = existing.WsdlUrl;

        existing.UpdateConfiguration(
            environment: command.Environment,
            emissionType: command.EmissionType,
            wsdlUrl: command.WsdlUrl,
            certPassword: certPasswordToPersist,
            updatedBy: userId
        );

        // CONFIG-FOUNDATION-P2-01: cada campo se compara y audita por separado — nunca se
        // guarda la contraseña real, solo un marcador Masked indicando que cambió.
        if (oldEnvironment != command.Environment)
            await LogFieldAsync(existing, "Environment", oldEnvironment.ToString(), command.Environment.ToString(), ConfigurationChangeValueType.Int, cancellationToken);
        if (oldEmissionType != command.EmissionType)
            await LogFieldAsync(existing, "EmissionType", oldEmissionType.ToString(), command.EmissionType.ToString(), ConfigurationChangeValueType.Int, cancellationToken);
        if (!string.Equals(oldWsdlUrl, existing.WsdlUrl, StringComparison.Ordinal))
            await LogFieldAsync(existing, "WsdlUrl", oldWsdlUrl, existing.WsdlUrl, ConfigurationChangeValueType.String, cancellationToken);
        if (hasNewPassword)
            await LogFieldAsync(existing, "CertPassword", "***", "***", ConfigurationChangeValueType.Masked, cancellationToken, isSensitive: true);

        await _repo.UpdateAsync(existing, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);
        return Result<SriConfigurationDto>.Success(ToDto(existing));
    }

    private Task LogFieldAsync(
        SriSettings config,
        string fieldName,
        string? oldValue,
        string? newValue,
        ConfigurationChangeValueType valueType,
        CancellationToken ct,
        bool isSensitive = false
    ) =>
        _changeLogger.LogAsync(
            new ConfigurationChangeLogEntry(
                TenantId: config.TenantId,
                CompanyId: config.CompanyId,
                Scope: OrgScope.Company,
                ScopeId: config.CompanyId,
                Key: null,
                EntityType: "SriSettings",
                EntityId: config.Id,
                FieldName: fieldName,
                OldValue: oldValue,
                NewValue: newValue,
                ValueType: valueType,
                ChangedBy: _currentUser.UserId,
                Source: ConfigurationChangeSource.Api,
                IsSensitive: isSensitive
            ),
            ct
        );

    private static SriConfigurationDto ToDto(SriSettings c) =>
        new(
            c.CompanyId,
            HasCertificate: !string.IsNullOrWhiteSpace(c.CertP12Path),
            c.CertFileName,
            c.CertSizeBytes,
            c.CertUploadedAtUtc,
            c.Environment,
            c.EmissionType,
            c.WsdlUrl
        );
}
