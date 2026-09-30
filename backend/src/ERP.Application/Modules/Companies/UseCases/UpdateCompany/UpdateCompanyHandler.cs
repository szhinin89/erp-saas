using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
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
        // Empresa pedida por id: inexistente, de otro tenant, sin membership o no operativa son la
        // MISMA respuesta (NOT_FOUND, mismo texto) — no se revela qué empresas existen. Se decide
        // por código: solo UNAUTHORIZED se propaga tal cual.
        if (!access.IsSuccess)
            return access.Code == ApiResponseCodes.Common.Unauthorized
                ? Result<CompanyDetailDto>.Failure(access.Error!, access.Code)
                : Result<CompanyDetailDto>.NotFound("Empresa no encontrada.");

        var entity = await _companies.GetTrackedByIdForTenantAsync(
            command.Id,
            access.Value!.TenantId,
            cancellationToken
        );
        if (entity is null)
            return Result<CompanyDetailDto>.NotFound("Empresa no encontrada.");

        return await CompanyIdentityUpdate.ApplyAsync(
            entity,
            command,
            command.IsActive,
            _companies,
            _currentUser.UserId,
            cancellationToken
        );
    }
}
