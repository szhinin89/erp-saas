using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.ElectronicInvoicing.DTOs;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;

namespace ERP.Application.Modules.ElectronicInvoicing.UseCases.UpsertSriConfiguration;

public sealed class UpsertSriConfigurationCommandHandler
    : IRequestHandler<UpsertSriConfigurationCommand, Result<SriConfigurationDto>>
{
    private readonly ISriSettingsRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany    _currentCompany;
    private readonly ICurrentUser       _currentUser;
    private readonly ISecretProtector   _secretProtector;

    public UpsertSriConfigurationCommandHandler(
        ISriSettingsRepository repo,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser,
        ISecretProtector secretProtector)
    {
        _repo              = repo;
        _currentTenant = currentTenant;
        _currentCompany    = currentCompany;
        _currentUser       = currentUser;
        _secretProtector   = secretProtector;
    }

    public async Task<Result<SriConfigurationDto>> Handle(
        UpsertSriConfigurationCommand command, CancellationToken cancellationToken)
    {
        var tenantId  = _currentTenant.TenantId;
        var companyId = _currentCompany.CompanyId;
        var userId    = _currentUser.UserId;
        var hasNewPassword = !string.IsNullOrWhiteSpace(command.CertPassword);

        var existing = await _repo.GetByCompanyIdAsync(companyId, cancellationToken);

        if (existing is null)
        {
            var config = SriSettings.Create(
                tenantId: tenantId,
                companyId:    companyId,
                environment:  command.Environment,
                emissionType: command.EmissionType,
                wsdlUrl:      command.WsdlUrl,
                createdBy:    userId);

            if (hasNewPassword)
            {
                config.UpdateConfiguration(
                    environment:  command.Environment,
                    emissionType: command.EmissionType,
                    wsdlUrl:      command.WsdlUrl,
                    certPassword: _secretProtector.Protect(command.CertPassword!.Trim()),
                    updatedBy:    userId);
            }

            await _repo.AddAsync(config, cancellationToken);
            await _repo.SaveChangesAsync(cancellationToken);
            return Result<SriConfigurationDto>.Success(ToDto(config));
        }

        // Contraseña vacía en una actualización = conservar la ya cifrada; solo se
        // re-encripta si el usuario efectivamente ingresó un valor nuevo.
        var certPasswordToPersist = hasNewPassword
            ? _secretProtector.Protect(command.CertPassword!.Trim())
            : null;

        existing.UpdateConfiguration(
            environment:  command.Environment,
            emissionType: command.EmissionType,
            wsdlUrl:      command.WsdlUrl,
            certPassword: certPasswordToPersist,
            updatedBy:    userId);

        await _repo.UpdateAsync(existing, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);
        return Result<SriConfigurationDto>.Success(ToDto(existing));
    }

    private static SriConfigurationDto ToDto(SriSettings c) => new(
        c.CompanyId,
        HasCertificate: !string.IsNullOrWhiteSpace(c.CertP12Path),
        c.CertFileName,
        c.CertSizeBytes,
        c.CertUploadedAtUtc,
        c.Environment,
        c.EmissionType,
        c.WsdlUrl);
}
