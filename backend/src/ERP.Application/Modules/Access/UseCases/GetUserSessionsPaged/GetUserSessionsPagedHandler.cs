using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Domain.Access.Enums;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Access.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.GetUserSessionsPaged;

public sealed class GetUserSessionsPagedHandler
    : IRequestHandler<GetUserSessionsPagedQuery, Result<PagedResult<UserSessionAdminDto>>>
{
    private readonly IUserSessionRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly IAccessRepository _accessRepository;
    private readonly IBranchRepository _branchRepository;

    public GetUserSessionsPagedHandler(
        IUserSessionRepository repository,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        IAccessRepository accessRepository,
        IBranchRepository branchRepository
    )
    {
        _repository = repository;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _accessRepository = accessRepository;
        _branchRepository = branchRepository;
    }

    public async Task<Result<PagedResult<UserSessionAdminDto>>> Handle(
        GetUserSessionsPagedQuery request,
        CancellationToken cancellationToken
    )
    {
        // IAM-V1-F1: siempre la empresa operativa del actor. Un CompanyId de la query distinto
        // no amplía el alcance (otra empresa del tenant nunca es visible desde aquí).
        if (!_currentCompany.HasCompanyContext)
            return Result<PagedResult<UserSessionAdminDto>>.Forbidden(
                "No hay una empresa activa en la sesión."
            );

        UserSessionStatus? status = null;
        if (
            !string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<UserSessionStatus>(request.Status, true, out var parsed)
        )
            status = parsed;

        var (items, total) = await _repository.GetPagedAsync(
            _currentTenant.TenantId,
            request.IdentityUserId,
            _currentCompany.CompanyId,
            status,
            request.FromUtc,
            request.ToUtc,
            request.PageNumber,
            request.PageSize,
            cancellationToken
        );

        // Nombres de solo lectura: los usuarios salen únicamente de las sesiones ya filtradas por
        // la empresa operativa (sin cuentas globales) y las sucursales de esa misma empresa.
        var users = (
            await _accessRepository.GetUsersByIdsAsync(
                items.Select(s => s.IdentityUserId).Distinct().ToList(),
                cancellationToken
            )
        ).ToDictionary(u => u.Id);
        var branches = (
            await _branchRepository.GetByCompanyAsync(
                _currentTenant.TenantId,
                _currentCompany.CompanyId,
                activeFilter: null,
                cancellationToken: cancellationToken
            )
        ).ToDictionary(b => b.Id, b => b.Name);

        var dtos = items
            .Select(s =>
                AccessSessionMapper.ToAdminDto(
                    s,
                    users.TryGetValue(s.IdentityUserId, out var u) ? u.FullName : null,
                    users.TryGetValue(s.IdentityUserId, out var u2) ? u2.Username : null,
                    branches.TryGetValue(s.BranchId, out var bn) ? bn : null
                )
            )
            .ToList();
        var result = new PagedResult<UserSessionAdminDto>(
            dtos,
            request.PageNumber,
            request.PageSize,
            total
        );

        return Result<PagedResult<UserSessionAdminDto>>.Success(result);
    }
}
