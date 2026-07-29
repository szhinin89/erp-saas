using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Domain.Access.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.LookupUserByUsernameAdmin;

public sealed class LookupUserByUsernameAdminHandler
    : IRequestHandler<LookupUserByUsernameAdminQuery, Result<UsernameLookupDto>>
{
    private readonly IAccessRepository _accessRepository;
    private readonly ICurrentCompany _currentCompany;

    public LookupUserByUsernameAdminHandler(
        IAccessRepository accessRepository,
        ICurrentCompany currentCompany
    )
    {
        _accessRepository = accessRepository;
        _currentCompany = currentCompany;
    }

    public async Task<Result<UsernameLookupDto>> Handle(
        LookupUserByUsernameAdminQuery request,
        CancellationToken cancellationToken
    )
    {
        if (!_currentCompany.HasCompanyContext)
            return Result<UsernameLookupDto>.Forbidden("No hay una empresa activa en la sesión.");

        var username = request.Username.Trim().ToLowerInvariant();
        var user = await _accessRepository.GetUserByUsernameAsync(username, cancellationToken);
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
