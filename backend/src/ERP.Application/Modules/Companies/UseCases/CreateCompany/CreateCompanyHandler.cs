using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Domain.Kernel.Security;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.CreateCompany;

public sealed class CreateCompanyHandler : IRequestHandler<CreateCompanyCommand, Result<CompanyDetailDto>>
{
    private readonly ICompanyAccessGuard         _accessGuard;
    private readonly ICompanyProvisioningService _provisioning;
    private readonly ICurrentUser                _currentUser;

    public CreateCompanyHandler(
        ICompanyAccessGuard accessGuard,
        ICompanyProvisioningService provisioning,
        ICurrentUser currentUser)
    {
        _accessGuard  = accessGuard;
        _provisioning = provisioning;
        _currentUser  = currentUser;
    }

    public async Task<Result<CompanyDetailDto>> Handle(CreateCompanyCommand command, CancellationToken cancellationToken)
    {
        var subResult = await _accessGuard.RequireActiveTenantAsync(cancellationToken);
        if (!subResult.IsSuccess)
            return Result<CompanyDetailDto>.Failure(subResult.Error!);

        try
        {
            var company = await _provisioning.CreateManagedCompanyAsync(
                subResult.Value!,
                command.TaxId,
                command.LegalName,
                mainAddress: "—",
                _currentUser.UserId,
                creatorRole: SecurityRoles.Admin,
                command.TradeName,
                command.CorporateEmail,
                phone: null,
                command.CountryCode,
                command.Timezone,
                command.CurrencyCode,
                command.BrandingJson,
                command.Website,
                cancellationToken);

            return Result<CompanyDetailDto>.Success(CompanyDetailDto.FromEntity(company));
        }
        catch (InvalidOperationException ex)
        {
            return Result<CompanyDetailDto>.Failure(ex.Message);
        }
    }
}
