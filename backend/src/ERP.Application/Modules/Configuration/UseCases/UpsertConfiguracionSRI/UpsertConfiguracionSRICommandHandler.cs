using MediatR;
using ERP.Application.Common;
using ERP.Application.Configuration.DTOs;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;

namespace ERP.Application.Configuration.UseCases.UpsertSriSettings;

public sealed class UpsertConfiguracionSRICommandHandler
    : IRequestHandler<UpsertConfiguracionSRICommand, Result<ConfiguracionSRIDto>>
{
    private readonly ISriSettingsRepository _repo;
    private readonly ICurrentTenant              _currentTenant;
    private readonly ICurrentUser                _currentUser;

    public UpsertConfiguracionSRICommandHandler(
        ISriSettingsRepository repo,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser)
    {
        _repo          = repo;
        _currentTenant = currentTenant;
        _currentUser   = currentUser;
    }

    public async Task<Result<ConfiguracionSRIDto>> Handle(
        UpsertConfiguracionSRICommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        var existing = await _repo.GetByTenantIdAsync(tenantId, ct);

        if (existing is null)
        {
            var config = SriSettings.Create(
                tenantId:          tenantId,
                ruc:               command.Ruc,
                legalName:         command.LegalName,
                tradeName:         command.TradeName,
                mainAddress:       command.MainAddress,
                requiresAccounting: command.RequiresAccounting,
                specialTaxpayer:   command.SpecialTaxpayer,
                estabCode:         command.EstabCode,
                emPointCode:       command.EmPointCode,
                currentSequential: 1,
                certP12Path:       command.CertP12Path,
                certPassword:      command.CertPassword,
                environment:       command.Environment,
                emissionType:      command.EmissionType,
                wsdlUrl:           command.WsdlUrl,
                createdBy: userId);

            await _repo.AddAsync(config, ct);
            await _repo.SaveChangesAsync(ct);
            return Result<ConfiguracionSRIDto>.Success(ToDto(config));
        }

        existing.Update(
            ruc:               command.Ruc,
            legalName:         command.LegalName,
            tradeName:         command.TradeName,
            mainAddress:       command.MainAddress,
            requiresAccounting: command.RequiresAccounting,
            specialTaxpayer:   command.SpecialTaxpayer,
            estabCode:         command.EstabCode,
            emPointCode:       command.EmPointCode,
            certP12Path:       command.CertP12Path,
            certPassword:      command.CertPassword,
            environment:       command.Environment,
            emissionType:      command.EmissionType,
            wsdlUrl:           command.WsdlUrl,
            updatedBy:         userId);

        await _repo.UpdateAsync(existing, ct);
        await _repo.SaveChangesAsync(ct);
        return Result<ConfiguracionSRIDto>.Success(ToDto(existing));
    }

    private static ConfiguracionSRIDto ToDto(SriSettings c) => new(
        c.TenantId, c.Ruc, c.LegalName, c.TradeName,
        c.MainAddress, c.RequiresAccounting, c.SpecialTaxpayer,
        c.EstabCode, c.EmPointCode, c.CurrentSequential,
        c.CertP12Path, c.Environment, c.EmissionType, c.WsdlUrl);
}
