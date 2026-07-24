using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Domain.Access.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.GetCompanyUserMembershipsAdmin;

/// <summary>
/// Fase I-C. Solo lectura: junta CompanyUserMembership + IdentityUser + AccessProfile, todos ya
/// expuestos individualmente por otros endpoints admin — no reimplementa ninguna regla de negocio
/// nueva, no escribe nada.
/// </summary>
public sealed class GetCompanyUserMembershipsAdminHandler
    : IRequestHandler<GetCompanyUserMembershipsAdminQuery, Result<IReadOnlyList<CompanyUserMembershipAdminDto>>>
{
    private readonly IAccessRepository _accessRepository;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentTenant _currentTenant;

    public GetCompanyUserMembershipsAdminHandler(
        IAccessRepository accessRepository, ICurrentCompany currentCompany, ICurrentTenant currentTenant)
    {
        _accessRepository = accessRepository;
        _currentCompany = currentCompany;
        _currentTenant = currentTenant;
    }

    public async Task<Result<IReadOnlyList<CompanyUserMembershipAdminDto>>> Handle(
        GetCompanyUserMembershipsAdminQuery request, CancellationToken cancellationToken)
    {
        if (!_currentCompany.HasCompanyContext)
            return Result<IReadOnlyList<CompanyUserMembershipAdminDto>>.Forbidden("No hay una empresa activa en la sesión.");

        var memberships = await _accessRepository.GetCompanyUserMembershipsByCompanyAsync(
            _currentCompany.CompanyId, request.OnlyActive, cancellationToken);

        if (memberships.Count == 0)
            return Result<IReadOnlyList<CompanyUserMembershipAdminDto>>.Success(Array.Empty<CompanyUserMembershipAdminDto>());

        var userIds = memberships.Select(m => m.IdentityUserId).Distinct().ToList();
        var usersById = (await _accessRepository.GetUsersByIdsAsync(userIds, cancellationToken))
            .ToDictionary(u => u.Id);

        var profilesById = (await _accessRepository.GetProfilesByTenantAsync(_currentTenant.TenantId, onlyActive: false, cancellationToken))
            .ToDictionary(p => p.Id);

        var dtos = memberships
            .Where(m => usersById.ContainsKey(m.IdentityUserId))
            .Select(m =>
            {
                var user = usersById[m.IdentityUserId];
                var profileName = m.ProfileId.HasValue && profilesById.TryGetValue(m.ProfileId.Value, out var profile)
                    ? profile.Name
                    : null;

                return new CompanyUserMembershipAdminDto(
                    m.Id, user.Id, user.Username, user.FullName, user.Email?.Value, m.Role, m.IsActive, m.ProfileId, profileName);
            })
            .OrderBy(d => d.FullName)
            .ToList();

        return Result<IReadOnlyList<CompanyUserMembershipAdminDto>>.Success(dtos);
    }
}
