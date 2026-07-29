using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.CreateCompanyUserPreferences;
using ERP.Application.Access.UseCases.GetCompanyUserPreferences;
using ERP.Application.Access.UseCases.UpdateCompanyUserPreferences;
using ERP.Application.Common;
using ERP.Domain.Access.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.UpdateCompanyUserPreferencesAdmin;

public sealed class UpdateCompanyUserPreferencesAdminHandler
    : IRequestHandler<
        UpdateCompanyUserPreferencesAdminCommand,
        Result<CompanyUserPreferencesAdminDto>
    >
{
    private readonly IAccessRepository _accessRepository;
    private readonly ICurrentCompany _currentCompany;
    private readonly IMediator _mediator;

    public UpdateCompanyUserPreferencesAdminHandler(
        IAccessRepository accessRepository,
        ICurrentCompany currentCompany,
        IMediator mediator
    )
    {
        _accessRepository = accessRepository;
        _currentCompany = currentCompany;
        _mediator = mediator;
    }

    public async Task<Result<CompanyUserPreferencesAdminDto>> Handle(
        UpdateCompanyUserPreferencesAdminCommand command,
        CancellationToken cancellationToken
    )
    {
        var membership = await _accessRepository.GetCompanyUserMembershipByIdAsync(
            command.CompanyUserId,
            cancellationToken
        );

        if (membership is null || membership.CompanyId != _currentCompany.CompanyId)
            return Result<CompanyUserPreferencesAdminDto>.NotFound(
                "Usuario de empresa no encontrado."
            );

        // Fase E: mismo criterio que UpdateCompanyUserBranchesAdminHandler — una membership
        // revocada no debe poder recibir cambios de preferencias operativas por esta vía.
        if (!membership.IsActive)
            return Result<CompanyUserPreferencesAdminDto>.Forbidden(
                "La membresía está revocada; no se pueden modificar sus preferencias."
            );

        // Fase G (hallazgo en pruebas manuales): membresías creadas antes de que existiera esta
        // infraestructura (o sembradas directamente en BD, ej. usuarios de setup) nunca pasaron por
        // el alta lazy de UpsertCompanyUserMembershipHandler y no tienen fila de preferencias. Mismo
        // criterio ya documentado ahí ("Nunca se deja una membresía sin fila de preferencias"): si no
        // existen, se crean en vez de fallar con NotFound.
        var existingPreferences = await _mediator.Send(
            new GetCompanyUserPreferencesQuery(command.CompanyUserId),
            cancellationToken
        );
        if (!existingPreferences.IsSuccess)
            return Result<CompanyUserPreferencesAdminDto>.Failure(
                existingPreferences.Error!,
                existingPreferences.Code
            );

        var updateResult = existingPreferences.Value is null
            ? await _mediator.Send(
                new CreateCompanyUserPreferencesCommand(
                    command.CompanyUserId,
                    command.LoginMode,
                    command.DefaultBranchId
                ),
                cancellationToken
            )
            : await _mediator.Send(
                new UpdateCompanyUserPreferencesCommand(
                    command.CompanyUserId,
                    command.LoginMode,
                    command.DefaultBranchId
                ),
                cancellationToken
            );

        if (!updateResult.IsSuccess)
            return Result<CompanyUserPreferencesAdminDto>.Failure(
                updateResult.Error!,
                updateResult.Code
            );

        var preferences = updateResult.Value!;
        var dto = new CompanyUserPreferencesAdminDto(
            preferences.CompanyUserMembershipId,
            preferences.DefaultBranchId,
            preferences.LoginMode
        );

        return Result<CompanyUserPreferencesAdminDto>.Success(dto);
    }
}
