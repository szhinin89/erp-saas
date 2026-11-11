using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Domain.Access.Enums;
using ERP.Domain.Access.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.GetUserSessionsPaged;

public sealed class GetUserSessionsPagedHandler
    : IRequestHandler<GetUserSessionsPagedQuery, Result<PagedResult<UserSessionAdminDto>>>
{
    private readonly IUserSessionRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;

    public GetUserSessionsPagedHandler(
        IUserSessionRepository repository,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany
    )
    {
        _repository = repository;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
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

        var dtos = items.Select(AccessSessionMapper.ToAdminDto).ToList();
        var result = new PagedResult<UserSessionAdminDto>(
            dtos,
            request.PageNumber,
            request.PageSize,
            total
        );

        return Result<PagedResult<UserSessionAdminDto>>.Success(result);
    }
}
