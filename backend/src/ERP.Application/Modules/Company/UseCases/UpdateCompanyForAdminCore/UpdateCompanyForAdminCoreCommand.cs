using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Application.Modules.Companies.UseCases.UpdateCompany;
using ERP.Domain.Modules.Company.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.UpdateCompanyForAdminCore;

/// <summary>
/// Edición de identidad de empresa desde la consola global (Admin Global, token sin tenant): RUC, razón
/// social, nombre comercial y estado de una empresa explícita de cualquier tenant. La razón social y el
/// nombre comercial comparten <c>CompanyNamesRules</c> con el perfil de la empresa activa; el RUC y el
/// estado solo se editan por este comando.
/// </summary>
public sealed record UpdateCompanyForAdminCoreCommand(
    Guid Id,
    string LegalName,
    string? TradeName,
    bool IsActive,
    string? TaxId
) : IRequest<Result<CompanyDetailDto>>, ICompanyIdentityInput;

public sealed class UpdateCompanyForAdminCoreCommandValidator
    : AbstractValidator<UpdateCompanyForAdminCoreCommand>
{
    public UpdateCompanyForAdminCoreCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        Include(new CompanyIdentityRules());
    }
}

public sealed class UpdateCompanyForAdminCoreHandler(
    ICompanyRepository companies,
    ICurrentUser user,
    ICurrentTenant tenant
) : IRequestHandler<UpdateCompanyForAdminCoreCommand, Result<CompanyDetailDto>>
{
    public async Task<Result<CompanyDetailDto>> Handle(
        UpdateCompanyForAdminCoreCommand command,
        CancellationToken cancellationToken
    )
    {
        // Defensa en profundidad además de la policy PlatformAdmin del endpoint.
        if (!user.IsAuthenticated || tenant.TenantId != Guid.Empty || user.Role != "Admin")
            return Result<CompanyDetailDto>.Forbidden(
                "Solo Admin Global puede editar empresas desde este endpoint."
            );

        var entity = await companies.GetTrackedByIdForAdminCoreAsync(command.Id, cancellationToken);
        if (entity is null)
            return Result<CompanyDetailDto>.NotFound("Empresa no encontrada.");

        return await CompanyIdentityUpdate.ApplyAsync(
            entity,
            command,
            command.IsActive,
            companies,
            user.UserId,
            cancellationToken
        );
    }
}
