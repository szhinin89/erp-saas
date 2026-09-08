using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Application.Modules.Companies.UseCases.UpdateCompany;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.UpdateCompanyForAdminCore;

public sealed record UpdateCompanyForAdminCoreCommand(
    Guid Id, string LegalName, string? TradeName, bool IsActive, string? TaxId
) : IRequest<Result<CompanyDetailDto>>;

public sealed class UpdateCompanyForAdminCoreHandler(
    ICompanyRepository companies, ICurrentUser user, ICurrentTenant tenant
) : IRequestHandler<UpdateCompanyForAdminCoreCommand, Result<CompanyDetailDto>>
{
    public async Task<Result<CompanyDetailDto>> Handle(
        UpdateCompanyForAdminCoreCommand command, CancellationToken cancellationToken)
    {
        if (!user.IsAuthenticated || tenant.TenantId != Guid.Empty || user.Role != "Admin")
            return Result<CompanyDetailDto>.Forbidden("Solo Admin Global puede editar empresas desde este endpoint.");

        var entity = await companies.GetTrackedByIdForAdminCoreAsync(command.Id, cancellationToken);
        if (entity is null)
            return Result<CompanyDetailDto>.Failure("Empresa no encontrada.");

        return await UpdateCompanyHandler.UpdateEntityAsync(
            new UpdateCompanyCommand(command.Id, command.LegalName, command.TradeName, command.IsActive, command.TaxId),
            entity, companies, user.UserId, cancellationToken);
    }
}
