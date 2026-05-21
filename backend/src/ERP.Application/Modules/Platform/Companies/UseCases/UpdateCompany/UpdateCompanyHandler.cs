using ERP.Application.Common;
using ERP.Application.Modules.Platform.Companies.DTOs;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Platform.Companies.UseCases.UpdateCompany;

public sealed class UpdateCompanyHandler : IRequestHandler<UpdateCompanyCommand, Result<CompanyDetailDto>>
{
    private readonly ICompanyAccessGuard _accessGuard;
    private readonly ICompanyRepository _companies;

    public UpdateCompanyHandler(ICompanyAccessGuard accessGuard, ICompanyRepository companies)
    {
        _accessGuard = accessGuard;
        _companies = companies;
    }

    public async Task<Result<CompanyDetailDto>> Handle(UpdateCompanyCommand command, CancellationToken ct)
    {
        var access = await _accessGuard.RequireMembershipAsync(command.Id, requireActiveCompany: false, ct);
        if (!access.IsSuccess)
            return Result<CompanyDetailDto>.Failure(access.Error!);

        var entity = await _companies.GetTrackedByIdForSubscriberAsync(command.Id, access.Value!.SubscriberId, ct);
        if (entity is null)
            return Result<CompanyDetailDto>.Failure("Empresa no encontrada.");

        if (!string.IsNullOrWhiteSpace(command.TaxId) && !string.Equals(command.TaxId.Trim(), entity.Ruc, StringComparison.Ordinal))
        {
            var taken = await _companies.GetByRucAsync(command.TaxId.Trim(), ct);
            if (taken is not null && taken.Id != entity.Id)
                return Result<CompanyDetailDto>.Failure("El RUC ya está registrado en el sistema.", ERP.Domain.Exceptions.CompanyRucAlreadyExistsException.ErrorCode);
            var isProvisional = ProvisionalTaxIdGenerator.IsProvisional(command.TaxId);
            entity.UpdateTaxId(
                command.TaxId,
                isProvisional,
                isProvisional ? TaxIdStatus.Pending : TaxIdStatus.Verified);
        }

        entity.UpdateProfile(
            command.LegalName,
            command.TradeName,
            command.MainAddress,
            command.Phone,
            command.Email,
            command.CountryCode,
            command.Timezone,
            command.CurrencyCode,
            command.LogoUrl,
            command.BrandingJson,
            command.IsActive);

        await _companies.SaveChangesAsync(ct);

        return Result<CompanyDetailDto>.Success(CompanyDetailDto.FromEntity(entity));
    }
}
