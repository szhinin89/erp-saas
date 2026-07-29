using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.UpdateCompanyDocuments;

public sealed class UpdateCompanyDocumentsHandler
    : IRequestHandler<UpdateCompanyDocumentsCommand, Result<CompanyProfileDto>>
{
    private readonly ICompanyAccessGuard _accessGuard;
    private readonly ICompanyRepository _companies;
    private readonly ICurrentUser _currentUser;

    public UpdateCompanyDocumentsHandler(
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
        UpdateCompanyDocumentsCommand command,
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

        entity.UpdateDocumentsSettings(command.ExtraLegend, _currentUser.UserId);

        await _companies.SaveChangesAsync(cancellationToken);

        return Result<CompanyProfileDto>.Success(CompanyProfileDto.FromEntity(entity));
    }
}
