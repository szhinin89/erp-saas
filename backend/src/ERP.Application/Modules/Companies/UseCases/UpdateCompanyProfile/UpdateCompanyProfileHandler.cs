using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.UpdateCompanyProfile;

public sealed class UpdateCompanyProfileHandler
    : IRequestHandler<UpdateCompanyProfileCommand, Result<CompanyProfileDto>>
{
    private readonly ICompanyAccessGuard _accessGuard;
    private readonly ICompanyRepository _companies;
    private readonly ICurrentUser _currentUser;

    public UpdateCompanyProfileHandler(
        ICompanyAccessGuard accessGuard,
        ICompanyRepository companies,
        ICurrentUser currentUser
    )
    {
        _accessGuard = accessGuard;
        _companies = companies;
        _currentUser = currentUser;
    }

    public async Task<Result<CompanyProfileDto>> Handle(
        UpdateCompanyProfileCommand command,
        CancellationToken cancellationToken
    )
    {
        var access = await _accessGuard.RequireCurrentCompanyAsync(cancellationToken);
        if (!access.IsSuccess)
            return Result<CompanyProfileDto>.Failure(access.Error!);

        var entity = await _companies.GetTrackedByIdForTenantAsync(
            access.Value!.CompanyId,
            access.Value!.TenantId,
            cancellationToken
        );
        if (entity is null)
            return Result<CompanyProfileDto>.Failure("Empresa no encontrada.");

        entity.UpdateContactProfile(
            command.CorporateEmail,
            command.Phone,
            command.Website,
            command.Timezone,
            command.CurrencyCode,
            command.LegalRepName,
            command.LegalRepPosition,
            command.LegalRepIdNumber,
            command.LegalRepEmail,
            command.LegalRepPhone,
            _currentUser.UserId
        );

        await _companies.SaveChangesAsync(cancellationToken);

        return Result<CompanyProfileDto>.Success(CompanyProfileDto.FromEntity(entity));
    }
}
