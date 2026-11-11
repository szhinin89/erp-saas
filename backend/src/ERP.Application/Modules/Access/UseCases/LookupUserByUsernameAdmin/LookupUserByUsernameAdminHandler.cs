using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Domain.Access.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.LookupUserByUsernameAdmin;

/// <summary>
/// IAM-V1-F1: solo revela identidades visibles desde la empresa operativa
/// (<see cref="IamIdentityScope.FindVisibleUserByUsernameAsync"/>): con membership en esta
/// empresa, o vinculables desde otra empresa del mismo tenant. Una cuenta global o de otro tenant
/// responde exactamente igual que un username inexistente (<c>exists=false</c>).
/// </summary>
public sealed class LookupUserByUsernameAdminHandler
    : IRequestHandler<LookupUserByUsernameAdminQuery, Result<UsernameLookupDto>>
{
    private readonly IAccessRepository _accessRepository;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentTenant _currentTenant;

    public LookupUserByUsernameAdminHandler(
        IAccessRepository accessRepository,
        ICurrentCompany currentCompany,
        ICurrentTenant currentTenant
    )
    {
        _accessRepository = accessRepository;
        _currentCompany = currentCompany;
        _currentTenant = currentTenant;
    }

    public async Task<Result<UsernameLookupDto>> Handle(
        LookupUserByUsernameAdminQuery request,
        CancellationToken cancellationToken
    )
    {
        if (!_currentCompany.HasCompanyContext)
            return Result<UsernameLookupDto>.Forbidden("No hay una empresa activa en la sesión.");

        var user = await IamIdentityScope.FindVisibleUserByUsernameAsync(
            _accessRepository,
            _currentTenant.TenantId,
            _currentCompany.CompanyId,
            request.Username,
            cancellationToken
        );
        if (user is null)
            return Result<UsernameLookupDto>.Success(new UsernameLookupDto(false, null, null));

        var membership = await _accessRepository.GetCompanyUserMembershipAsync(
            _currentCompany.CompanyId,
            user.Id,
            cancellationToken
        );
        var membershipDto = membership is null
            ? null
            : new UsernameMembershipLookupDto(
                membership.Id,
                membership.IsActive,
                membership.Role,
                membership.ProfileId
            );

        return Result<UsernameLookupDto>.Success(
            new UsernameLookupDto(true, user.FullName, membershipDto)
        );
    }
}
