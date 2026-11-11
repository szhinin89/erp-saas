using ERP.Application.Access.UseCases;
using ERP.Application.Audit.DTOs;
using ERP.Application.Common;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;

namespace ERP.Application.Audit.UseCases.GetEntityActivity;

/// <summary>
/// Actividad de una entidad para la sesión empresarial. <c>UserActivity</c> solo guarda
/// <c>TenantId</c>, así que el aislamiento por empresa se decide sobre la ENTIDAD consultada
/// (IAM-V1-F1, gate final), reutilizando los repositorios con alcance de empresa ya existentes:
/// <list type="bullet">
///   <item><c>Branch</c>/<c>Warehouse</c>: debe existir en la empresa activa
///   (<c>GetByIdForCompanyAsync</c>).</item>
///   <item><c>BusinessPartner</c>: dato TENANT compartido entre empresas del tenant.</item>
///   <item><c>IdentityUser</c>/<c>CompanyUserMembership</c>: administrable desde la empresa activa
///   (<see cref="IamIdentityScope"/>; nunca una cuenta global).</item>
///   <item>Cualquier otro tipo: fail-closed.</item>
/// </list>
/// Una entidad fuera de alcance responde igual que una entidad sin actividad (lista vacía). Los
/// registros históricos no se modifican; los hechos por una cuenta global se muestran con actor
/// neutro.
/// </summary>
public class GetEntityActivityHandler
    : IRequestHandler<GetEntityActivityQuery, Result<IReadOnlyList<UserActivityDto>>>
{
    /// <summary>Tipos que registran los escritores actuales de <c>UserActivity</c>.</summary>
    public const string BranchEntityType = "Branch";
    public const string WarehouseEntityType = "Warehouse";
    public const string BusinessPartnerEntityType = "BusinessPartner";
    public const string IdentityUserEntityType = "IdentityUser";
    public const string CompanyUserMembershipEntityType = "CompanyUserMembership";

    /// <summary>
    /// IAM-V1-F1: actor neutro para registros hechos por una cuenta global. El registro de
    /// auditoría se conserva íntegro en base; solo la consulta empresarial no revela la identidad.
    /// </summary>
    public const string GlobalActorDisplayName = "Administración del sistema";

    private readonly IUserActivityRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentCompany _currentCompany;
    private readonly IAccessRepository _accessRepository;
    private readonly IBranchRepository _branchRepository;
    private readonly IWarehouseRepository _warehouseRepository;

    public GetEntityActivityHandler(
        IUserActivityRepository repo,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        ICurrentCompany currentCompany,
        IAccessRepository accessRepository,
        IBranchRepository branchRepository,
        IWarehouseRepository warehouseRepository
    )
    {
        _repo = repo;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
        _currentCompany = currentCompany;
        _accessRepository = accessRepository;
        _branchRepository = branchRepository;
        _warehouseRepository = warehouseRepository;
    }

    public async Task<Result<IReadOnlyList<UserActivityDto>>> Handle(
        GetEntityActivityQuery query,
        CancellationToken cancellationToken
    )
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
            return Result<IReadOnlyList<UserActivityDto>>.Failure("No autenticado.");

        if (query.EntityId == Guid.Empty)
            return Result<IReadOnlyList<UserActivityDto>>.Failure("EntityId requerido.");

        if (string.IsNullOrWhiteSpace(query.EntityType))
            return Result<IReadOnlyList<UserActivityDto>>.Failure("EntityType requerido.");

        if (!_currentCompany.HasCompanyContext)
            return Result<IReadOnlyList<UserActivityDto>>.Forbidden(
                "No hay una empresa activa en la sesión."
            );

        var take =
            query.Take < 1 ? 10
            : query.Take > 50 ? 50
            : query.Take;
        var entityType = query.EntityType.Trim();

        if (!await IsEntityInCompanyScopeAsync(entityType, query.EntityId, cancellationToken))
            return Result<IReadOnlyList<UserActivityDto>>.Success(
                Array.Empty<UserActivityDto>()
            );

        var list = await _repo.GetByEntityAsync(
            _currentTenant.TenantId,
            entityType,
            query.EntityId,
            take,
            cancellationToken
        );

        var globalActorIds = await IamIdentityScope.GetGlobalUserIdsAsync(
            _accessRepository,
            list.Select(x => x.UserId),
            cancellationToken
        );

        var dto = list.Select(x =>
            {
                var isGlobalActor = globalActorIds.Contains(x.UserId);
                return new UserActivityDto(
                    x.Id,
                    x.Module,
                    x.Action,
                    x.EntityType,
                    x.EntityId,
                    x.Description,
                    x.CreatedAt,
                    isGlobalActor ? null : x.UserEmail,
                    isGlobalActor ? GlobalActorDisplayName : x.UserFullName
                );
            })
            .ToList();

        return Result<IReadOnlyList<UserActivityDto>>.Success(dto);
    }

    private async Task<bool> IsEntityInCompanyScopeAsync(
        string entityType,
        Guid entityId,
        CancellationToken ct
    )
    {
        var tenantId = _currentTenant.TenantId;
        var companyId = _currentCompany.CompanyId;

        return entityType switch
        {
            BranchEntityType => await _branchRepository.GetByIdForCompanyAsync(
                tenantId,
                companyId,
                entityId,
                ct
            )
                is not null,
            WarehouseEntityType => await _warehouseRepository.GetByIdForCompanyAsync(
                tenantId,
                companyId,
                entityId,
                ct
            )
                is not null,
            // Dato TENANT: compartido por las empresas del tenant (el repositorio ya filtra tenant).
            BusinessPartnerEntityType => true,
            IdentityUserEntityType => await IamIdentityScope.GetAdministrableMembershipAsync(
                _accessRepository,
                companyId,
                entityId,
                ct
            )
                is not null,
            CompanyUserMembershipEntityType =>
                await IamIdentityScope.IsAdministrableMembershipAsync(
                    _accessRepository,
                    await _accessRepository.GetCompanyUserMembershipByIdAsync(entityId, ct),
                    companyId,
                    ct
                ),
            _ => false,
        };
    }
}
