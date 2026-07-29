using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Domain.Access.Enums;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Branches.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.UpdateCompanyUserPreferences;

public sealed class UpdateCompanyUserPreferencesHandler
    : IRequestHandler<UpdateCompanyUserPreferencesCommand, Result<CompanyUserPreferencesDto>>
{
    private readonly ICompanyUserPreferencesRepository _preferencesRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly ICompanyUserBranchRepository _companyUserBranchRepository;
    private readonly ICurrentUser _currentUser;

    public UpdateCompanyUserPreferencesHandler(
        ICompanyUserPreferencesRepository preferencesRepository,
        IBranchRepository branchRepository,
        ICompanyUserBranchRepository companyUserBranchRepository,
        ICurrentUser currentUser)
    {
        _preferencesRepository = preferencesRepository;
        _branchRepository = branchRepository;
        _companyUserBranchRepository = companyUserBranchRepository;
        _currentUser = currentUser;
    }

    public async Task<Result<CompanyUserPreferencesDto>> Handle(
        UpdateCompanyUserPreferencesCommand command, CancellationToken cancellationToken)
    {
        var existing = await _preferencesRepository.GetByMembershipAsync(
            command.CompanyUserMembershipId, cancellationToken);
        if (existing is null)
            return Result<CompanyUserPreferencesDto>.NotFound(
                "No existen preferencias para esta membresía. Créelas primero con CreateCompanyUserPreferences.");

        var loginMode = Enum.Parse<CompanyUserLoginMode>(command.LoginMode);

        var branchValidation = await CompanyUserPreferencesDefaultBranchValidation.ValidateAsync(
            _branchRepository, _companyUserBranchRepository,
            existing.TenantId, existing.CompanyUserMembershipId, command.DefaultBranchId, loginMode, cancellationToken);
        if (branchValidation is not null)
            return branchValidation;

        // Orden deliberado: si el modo destino es DirectToDefault, la sucursal debe quedar
        // asignada ANTES de activar el modo (el agregado exige DefaultBranchId no nulo para
        // aceptar DirectToDefault). Si el modo destino es AskBranch, se afloja el modo ANTES de
        // poder limpiar la sucursal (el agregado rechaza limpiar DefaultBranchId mientras siga en
        // DirectToDefault). Ninguna de las dos entidades de dominio se modificó para esto.
        if (loginMode == CompanyUserLoginMode.DirectToDefault)
        {
            existing.ChangeDefaultBranch(command.DefaultBranchId, _currentUser.UserId);
            existing.ChangeLoginMode(loginMode, _currentUser.UserId);
        }
        else
        {
            existing.ChangeLoginMode(loginMode, _currentUser.UserId);
            existing.ChangeDefaultBranch(command.DefaultBranchId, _currentUser.UserId);
        }

        await _preferencesRepository.SaveChangesAsync(cancellationToken);

        return Result<CompanyUserPreferencesDto>.Success(CompanyUserPreferencesMapper.ToDto(existing));
    }
}
