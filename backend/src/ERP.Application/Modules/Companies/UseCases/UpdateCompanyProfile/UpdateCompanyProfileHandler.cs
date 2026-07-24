using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.UpdateCompanyProfile;

public sealed class UpdateCompanyProfileHandler : IRequestHandler<UpdateCompanyProfileCommand, Result<CompanyProfileDto>>
{
    private readonly ICompanyAccessGuard _accessGuard;
    private readonly ICompanyRepository _companies;
    private readonly ICurrentUser _currentUser;

    public UpdateCompanyProfileHandler(ICompanyAccessGuard accessGuard, ICompanyRepository companies, ICurrentUser currentUser)
    {
        _accessGuard = accessGuard;
        _companies = companies;
        _currentUser = currentUser;
    }

    public async Task<Result<CompanyProfileDto>> Handle(UpdateCompanyProfileCommand command, CancellationToken cancellationToken)
    {
        var access = await _accessGuard.RequireCurrentCompanyAsync(cancellationToken);
        if (!access.IsSuccess)
            return Result<CompanyProfileDto>.Failure(access.Error!);

        var entity = await _companies.GetTrackedByIdForTenantAsync(access.Value!.CompanyId, access.Value!.TenantId, cancellationToken);
        if (entity is null)
            return Result<CompanyProfileDto>.Failure("Empresa no encontrada.");

        if (!string.IsNullOrWhiteSpace(command.TaxIdentificationNumber) &&
            !string.Equals(command.TaxIdentificationNumber.Trim(), entity.TaxIdentificationNumber, StringComparison.Ordinal))
        {
            var taken = await _companies.GetByTaxIdentificationNumberAsync(command.TaxIdentificationNumber.Trim(), cancellationToken);
            if (taken is not null && taken.Id != entity.Id)
                return Result<CompanyProfileDto>.Failure("El RUC ya está registrado en el sistema.", ERP.Domain.Exceptions.CompanyRucAlreadyExistsException.ErrorCode);

            var isTemporary = ProvisionalTaxIdGenerator.IsProvisional(command.TaxIdentificationNumber);
            entity.UpdateTaxIdentification(
                command.TaxIdentificationNumber,
                isTemporary,
                isTemporary ? TaxIdentificationStatus.Pending : TaxIdentificationStatus.Verified,
                _currentUser.UserId);
        }

        entity.UpdateProfile(
            command.LegalName,
            command.TradeName,
            command.CorporateEmail,
            command.Website,
            entity.CountryCode,
            command.Timezone,
            command.CurrencyCode,
            _currentUser.UserId,
            command.Phone,
            command.LegalRepName,
            command.LegalRepPosition,
            command.LegalRepIdNumber,
            command.LegalRepEmail,
            command.LegalRepPhone);

        await _companies.SaveChangesAsync(cancellationToken);

        return Result<CompanyProfileDto>.Success(CompanyProfileDto.FromEntity(entity));
    }
}
