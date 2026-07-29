using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.UpdateCompany;

public sealed class UpdateCompanyHandler
    : IRequestHandler<UpdateCompanyCommand, Result<CompanyDetailDto>>
{
    private readonly ICompanyAccessGuard _accessGuard;
    private readonly ICompanyRepository _companies;
    private readonly ICurrentUser _currentUser;

    public UpdateCompanyHandler(
        ICompanyAccessGuard accessGuard,
        ICompanyRepository companies,
        ICurrentUser currentUser
    )
    {
        _accessGuard = accessGuard;
        _companies = companies;
        _currentUser = currentUser;
    }

    public async Task<Result<CompanyDetailDto>> Handle(
        UpdateCompanyCommand command,
        CancellationToken cancellationToken
    )
    {
        var access = await _accessGuard.RequireMembershipAsync(
            command.Id,
            requireActiveCompany: false,
            cancellationToken
        );
        if (!access.IsSuccess)
            return Result<CompanyDetailDto>.Failure(access.Error!);

        var entity = await _companies.GetTrackedByIdForTenantAsync(
            command.Id,
            access.Value!.TenantId,
            cancellationToken
        );
        if (entity is null)
            return Result<CompanyDetailDto>.Failure("Empresa no encontrada.");

        if (
            !string.IsNullOrWhiteSpace(command.TaxId)
            && !string.Equals(
                command.TaxId.Trim(),
                entity.TaxIdentificationNumber,
                StringComparison.Ordinal
            )
        )
        {
            var taken = await _companies.GetByTaxIdentificationNumberAsync(
                command.TaxId.Trim(),
                cancellationToken
            );
            if (taken is not null && taken.Id != entity.Id)
                return Result<CompanyDetailDto>.Failure(
                    "El RUC ya está registrado en el sistema.",
                    ERP.Domain.Exceptions.CompanyRucAlreadyExistsException.ErrorCode
                );
            var isProvisional = ProvisionalTaxIdGenerator.IsProvisional(command.TaxId);
            entity.UpdateTaxIdentification(
                command.TaxId,
                isProvisional,
                isProvisional ? TaxIdentificationStatus.Pending : TaxIdentificationStatus.Verified,
                _currentUser.UserId
            );
        }

        entity.UpdateProfile(
            command.LegalName,
            command.TradeName,
            command.CorporateEmail,
            command.Website,
            command.CountryCode,
            command.Timezone,
            command.CurrencyCode,
            command.BrandingJson,
            command.IsActive,
            _currentUser.UserId
        );

        await _companies.SaveChangesAsync(cancellationToken);

        return Result<CompanyDetailDto>.Success(CompanyDetailDto.FromEntity(entity));
    }
}
