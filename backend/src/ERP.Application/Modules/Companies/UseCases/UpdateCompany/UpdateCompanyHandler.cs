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

        return await UpdateEntityAsync(command, entity, _companies, _currentUser.UserId, cancellationToken);
    }

    internal static async Task<Result<CompanyDetailDto>> UpdateEntityAsync(
        UpdateCompanyCommand command,
        ERP.Domain.Modules.Company.Entities.Company entity,
        ICompanyRepository companies,
        Guid userId,
        CancellationToken cancellationToken
    )
    {
        var validation = await new UpdateCompanyCommandValidator().ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return Result<CompanyDetailDto>.Failure(validation.Errors[0].ErrorMessage);

        if (
            !string.IsNullOrWhiteSpace(command.TaxId)
            && !string.Equals(
                command.TaxId.Trim(),
                entity.TaxIdentificationNumber,
                StringComparison.Ordinal
            )
        )
        {
            var taken = await companies.GetByTaxIdentificationNumberAsync(
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
                userId
            );
        }

        entity.UpdateAdminIdentity(
            command.LegalName,
            command.TradeName,
            command.IsActive,
            userId
        );

        await companies.SaveChangesAsync(cancellationToken);

        return Result<CompanyDetailDto>.Success(CompanyDetailDto.FromEntity(entity));
    }
}
