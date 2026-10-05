using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using CompanyEntity = ERP.Domain.Modules.Company.Entities.Company;

namespace ERP.Application.Modules.Companies.UseCases.UpdateCompany;

/// <summary>
/// ZH-COMPANY-IDENTITY-SSOT-01 — única operación de aplicación que cambia la identidad de una
/// empresa (RUC, razón social, nombre comercial, activo). La usan los dos contextos de
/// autorización, cada uno después de resolver su propio alcance:
/// <list type="bullet">
///   <item><see cref="UpdateCompanyHandler"/> — usuario del tenant, empresa con membership, lectura
///   acotada al tenant resuelto server-side;</item>
///   <item><c>UpdateCompanyForAdminCoreHandler</c> — Admin Global, empresa explícita de cualquier
///   tenant.</item>
/// </list>
/// El formato ya fue validado por el pipeline (<see cref="CompanyIdentityRules"/>); aquí vive la
/// regla que requiere datos: unicidad global del RUC. Los cambios de estado son del dominio
/// (<c>Company.UpdateTaxIdentification</c> / <c>Company.UpdateAdminIdentity</c>: normalización y
/// auditoría <c>UpdatedBy</c>).
/// </summary>
internal static class CompanyIdentityUpdate
{
    public static async Task<Result<CompanyDetailDto>> ApplyAsync(
        CompanyEntity entity,
        ICompanyIdentityInput identity,
        bool isActive,
        ICompanyRepository companies,
        Guid updatedBy,
        CancellationToken cancellationToken
    )
    {
        var taxId = identity.TaxId?.Trim();
        if (
            !string.IsNullOrEmpty(taxId)
            && !string.Equals(taxId, entity.TaxIdentificationNumber, StringComparison.Ordinal)
        )
        {
            var taken = await companies.GetByTaxIdentificationNumberAsync(taxId, cancellationToken);
            if (taken is not null && taken.Id != entity.Id)
                return Result<CompanyDetailDto>.Failure(
                    "El RUC ya está registrado en el sistema.",
                    ERP.Domain.Exceptions.CompanyRucAlreadyExistsException.ErrorCode
                );
            var isProvisional = ProvisionalTaxIdGenerator.IsProvisional(taxId);
            entity.UpdateTaxIdentification(
                taxId,
                isProvisional,
                isProvisional ? TaxIdentificationStatus.Pending : TaxIdentificationStatus.Verified,
                updatedBy
            );
        }

        entity.UpdateAdminIdentity(identity.LegalName, identity.TradeName, isActive, updatedBy);

        await companies.SaveChangesAsync(cancellationToken);
        return Result<CompanyDetailDto>.Success(CompanyDetailDto.FromEntity(entity));
    }
}
