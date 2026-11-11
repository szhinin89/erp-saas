using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.GetCompanyUserPreferences;
using ERP.Application.Common;
using ERP.Domain.Access.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.GetCompanyUserPreferencesAdmin;

public sealed class GetCompanyUserPreferencesAdminHandler
    : IRequestHandler<GetCompanyUserPreferencesAdminQuery, Result<CompanyUserPreferencesAdminDto?>>
{
    private readonly IAccessRepository _accessRepository;
    private readonly ICurrentCompany _currentCompany;
    private readonly IMediator _mediator;

    public GetCompanyUserPreferencesAdminHandler(
        IAccessRepository accessRepository,
        ICurrentCompany currentCompany,
        IMediator mediator
    )
    {
        _accessRepository = accessRepository;
        _currentCompany = currentCompany;
        _mediator = mediator;
    }

    public async Task<Result<CompanyUserPreferencesAdminDto?>> Handle(
        GetCompanyUserPreferencesAdminQuery request,
        CancellationToken cancellationToken
    )
    {
        var membership = await _accessRepository.GetCompanyUserMembershipByIdAsync(
            request.CompanyUserId,
            cancellationToken
        );

        // Mismo mensaje para "no existe" y "pertenece a otra empresa" — evita que un
        // administrador de un tenant/empresa distinto pueda confirmar por diferencia de
        // respuesta que un CompanyUserId de otra empresa existe.
        // IAM-V1-F1: una cuenta global responde igual que "no existe" / "otra empresa".
        if (
            !await IamIdentityScope.IsAdministrableMembershipAsync(
                _accessRepository,
                membership,
                _currentCompany.CompanyId,
                cancellationToken
            )
            || membership is null
        )
            return Result<CompanyUserPreferencesAdminDto?>.NotFound(
                "Usuario de empresa no encontrado."
            );

        var preferencesResult = await _mediator.Send(
            new GetCompanyUserPreferencesQuery(request.CompanyUserId),
            cancellationToken
        );
        if (!preferencesResult.IsSuccess)
            return Result<CompanyUserPreferencesAdminDto?>.Failure(
                preferencesResult.Error!,
                preferencesResult.Code
            );

        var preferences = preferencesResult.Value;
        var dto = preferences is null
            ? null
            : new CompanyUserPreferencesAdminDto(
                preferences.CompanyUserMembershipId,
                preferences.DefaultBranchId,
                preferences.LoginMode
            );

        return Result<CompanyUserPreferencesAdminDto?>.Success(dto);
    }
}
