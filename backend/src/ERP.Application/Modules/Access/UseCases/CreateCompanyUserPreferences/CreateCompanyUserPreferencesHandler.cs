using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Enums;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.CreateCompanyUserPreferences;

public sealed class CreateCompanyUserPreferencesHandler
    : IRequestHandler<CreateCompanyUserPreferencesCommand, Result<CompanyUserPreferencesDto>>
{
    private readonly IAccessRepository _accessRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly ICompanyUserBranchRepository _companyUserBranchRepository;
    private readonly ICompanyUserPreferencesRepository _preferencesRepository;
    private readonly ICurrentUser _currentUser;

    public CreateCompanyUserPreferencesHandler(
        IAccessRepository accessRepository,
        ICompanyRepository companyRepository,
        IBranchRepository branchRepository,
        ICompanyUserBranchRepository companyUserBranchRepository,
        ICompanyUserPreferencesRepository preferencesRepository,
        ICurrentUser currentUser
    )
    {
        _accessRepository = accessRepository;
        _companyRepository = companyRepository;
        _branchRepository = branchRepository;
        _companyUserBranchRepository = companyUserBranchRepository;
        _preferencesRepository = preferencesRepository;
        _currentUser = currentUser;
    }

    public async Task<Result<CompanyUserPreferencesDto>> Handle(
        CreateCompanyUserPreferencesCommand command,
        CancellationToken cancellationToken
    )
    {
        var membership = await _accessRepository.GetCompanyUserMembershipByIdAsync(
            command.CompanyUserMembershipId,
            cancellationToken
        );
        if (membership is null)
            return Result<CompanyUserPreferencesDto>.NotFound("La membresía no existe.");

        if (
            await _preferencesRepository.ExistsAsync(
                command.CompanyUserMembershipId,
                cancellationToken
            )
        )
            return Result<CompanyUserPreferencesDto>.Conflict(
                "Ya existen preferencias para esta membresía. Use UpdateCompanyUserPreferences."
            );

        var loginMode = Enum.Parse<CompanyUserLoginMode>(command.LoginMode);

        var company = await _companyRepository.GetByIdAsync(
            membership.CompanyId,
            cancellationToken
        );
        if (company is null)
            return Result<CompanyUserPreferencesDto>.NotFound(
                "La empresa de la membresía no existe."
            );

        var branchValidation = await CompanyUserPreferencesDefaultBranchValidation.ValidateAsync(
            _branchRepository,
            _companyUserBranchRepository,
            company.TenantId,
            membership.CompanyId,
            membership.Id,
            command.DefaultBranchId,
            loginMode,
            cancellationToken
        );
        if (branchValidation is not null)
            return branchValidation;

        var preferences = CompanyUserPreferences.Create(
            company.TenantId,
            membership.CompanyId,
            membership.Id,
            loginMode,
            command.DefaultBranchId,
            _currentUser.UserId
        );

        await _preferencesRepository.AddAsync(preferences, cancellationToken);
        await _preferencesRepository.SaveChangesAsync(cancellationToken);

        return Result<CompanyUserPreferencesDto>.Success(
            CompanyUserPreferencesMapper.ToDto(preferences)
        );
    }
}
