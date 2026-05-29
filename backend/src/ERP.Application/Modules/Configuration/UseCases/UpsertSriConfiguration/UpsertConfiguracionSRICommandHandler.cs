using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Configuration.DTOs;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;

namespace ERP.Application.Configuration.UseCases.UpsertSriSettings;

public sealed class UpsertSriConfigurationCommandHandler
    : IRequestHandler<UpsertSriConfigurationCommand, Result<SriConfigurationDto>>
{
    private readonly ISriSettingsRepository _repo;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentCompany    _currentCompany;
    private readonly ICurrentUser       _currentUser;
    private readonly ISecretProtector   _secretProtector;

    public UpsertSriConfigurationCommandHandler(
        ISriSettingsRepository repo,
        ICurrentSubscriber currentSubscriber,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser,
        ISecretProtector secretProtector)
    {
        _repo              = repo;
        _currentSubscriber = currentSubscriber;
        _currentCompany    = currentCompany;
        _currentUser       = currentUser;
        _secretProtector   = secretProtector;
    }

    public async Task<Result<SriConfigurationDto>> Handle(
        UpsertSriConfigurationCommand command, CancellationToken ct)
    {
        var subscriberId      = _currentSubscriber.SubscriberId;
        var companyId         = _currentCompany.CompanyId;
        var userId            = _currentUser.UserId;
        var protectedPassword = _secretProtector.Protect(command.CertPassword.Trim());

        var existing = await _repo.GetByCompanyIdAsync(companyId, ct);

        if (existing is null)
        {
            var config = SriSettings.Create(
                subscriberId: subscriberId,
                companyId:    companyId,
                certP12Path:  command.CertP12Path,
                certPassword: protectedPassword,
                environment:  command.Environment,
                emissionType: command.EmissionType,
                wsdlUrl:      command.WsdlUrl,
                createdBy:    userId);

            await _repo.AddAsync(config, ct);
            await _repo.SaveChangesAsync(ct);
            return Result<SriConfigurationDto>.Success(ToDto(config));
        }

        existing.Update(
            certP12Path:  command.CertP12Path,
            certPassword: protectedPassword,
            environment:  command.Environment,
            emissionType: command.EmissionType,
            wsdlUrl:      command.WsdlUrl,
            updatedBy:    userId);

        await _repo.UpdateAsync(existing, ct);
        await _repo.SaveChangesAsync(ct);
        return Result<SriConfigurationDto>.Success(ToDto(existing));
    }

    private static SriConfigurationDto ToDto(SriSettings c) => new(
        c.CompanyId, c.CertP12Path, c.Environment, c.EmissionType, c.WsdlUrl);
}
