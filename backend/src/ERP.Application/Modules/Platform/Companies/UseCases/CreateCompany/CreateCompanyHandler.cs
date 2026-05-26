using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Platform.Companies.DTOs;
using ERP.Domain.Subscriptions.Exceptions;
using MediatR;

namespace ERP.Application.Modules.Platform.Companies.UseCases.CreateCompany;

public sealed class CreateCompanyHandler : IRequestHandler<CreateCompanyCommand, Result<CompanyDetailDto>>
{
    private readonly ICompanyAccessGuard         _accessGuard;
    private readonly ICompanyProvisioningService _provisioning;
    private readonly ICompanyBootstrapService    _bootstrap;
    private readonly ICurrentUser                _currentUser;

    public CreateCompanyHandler(
        ICompanyAccessGuard accessGuard,
        ICompanyProvisioningService provisioning,
        ICompanyBootstrapService bootstrap,
        ICurrentUser currentUser)
    {
        _accessGuard  = accessGuard;
        _provisioning = provisioning;
        _bootstrap    = bootstrap;
        _currentUser  = currentUser;
    }

    public async Task<Result<CompanyDetailDto>> Handle(CreateCompanyCommand command, CancellationToken ct)
    {
        var subResult = await _accessGuard.RequireActiveSubscriberAsync(ct);
        if (!subResult.IsSuccess)
            return Result<CompanyDetailDto>.Failure(subResult.Error!);

        try
        {
            var company = await _provisioning.CreateManagedCompanyAsync(
                subResult.Value!,
                command.TaxId,
                command.LegalName,
                command.MainAddress,
                _currentUser.UserId,
                creatorRole: "Admin",
                command.TradeName,
                command.Email,
                command.Phone,
                command.CountryCode,
                command.Timezone,
                command.CurrencyCode,
                command.LogoUrl,
                command.BrandingJson,
                ct);

            await _bootstrap.BootstrapCompanyAsync(company.SubscriberId, company.Id, _currentUser.UserId, ct);

            return Result<CompanyDetailDto>.Success(CompanyDetailDto.FromEntity(company));
        }
        catch (CommercialPlanLimitExceededException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            return Result<CompanyDetailDto>.Failure(ex.Message);
        }
    }
}
