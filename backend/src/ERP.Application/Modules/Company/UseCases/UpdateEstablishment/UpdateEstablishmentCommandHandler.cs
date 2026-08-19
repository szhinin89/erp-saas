using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;
using ERP.Application.Modules.Company.UseCases.GetEstablishments;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.UpdateEstablishment;

/// <summary>
/// CONFIG-FOUNDATION-P2-01: antes de esta entrega, este handler llamaba a
/// <c>entity.SetMain(command.IsMain, ...)</c> sin desmarcar el establecimiento principal anterior
/// — un gap encontrado durante la auditoría de este bloque (a diferencia de Branch/EmissionPoint,
/// que sí tenían su ClearMainExcept/ClearDefaultExcept correspondiente). Corregido aquí con
/// IEstablishmentRepository.ClearMainExceptAsync, mismo patrón que sus pares, porque sin el flip
/// real no hay "entidad anterior desmarcada" que auditar y el cambio habría chocado con
/// uq_establishment_tenant_company_main (CONFIG-FOUNDATION-P0-01) con una excepción cruda.
/// </summary>
public sealed class UpdateEstablishmentCommandHandler
    : IRequestHandler<UpdateEstablishmentCommand, Result<EstablishmentDto>>
{
    private readonly IEstablishmentRepository _repo;
    private readonly IConfigurationChangeLogger _changeLogger;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _user;

    public UpdateEstablishmentCommandHandler(
        IEstablishmentRepository repo,
        IConfigurationChangeLogger changeLogger,
        ICurrentTenant tenant,
        ICurrentUser user
    )
    {
        _repo = repo;
        _changeLogger = changeLogger;
        _currentTenant = tenant;
        _user = user;
    }

    public async Task<Result<EstablishmentDto>> Handle(
        UpdateEstablishmentCommand command,
        CancellationToken cancellationToken
    )
    {
        var tenantId = _currentTenant.TenantId;
        var entity = await _repo.GetByIdAsync(tenantId, command.Id, cancellationToken);
        if (entity is null)
            return Result<EstablishmentDto>.Failure("Establecimiento no encontrado.");

        var wasMain = entity.IsMain;

        if (command.IsMain && !entity.IsMain)
        {
            var clearedIds = await _repo.ClearMainExceptAsync(
                tenantId,
                entity.CompanyId,
                command.Id,
                _user.UserId,
                cancellationToken
            );
            foreach (var clearedId in clearedIds)
                await LogMainChangeAsync(entity.CompanyId, clearedId, true, false, cancellationToken);
        }

        entity.Update(command.Name, command.Address, command.Phone, _user.UserId);
        entity.SetMain(command.IsMain, _user.UserId);

        if (wasMain != command.IsMain)
            await LogMainChangeAsync(entity.CompanyId, entity.Id, wasMain, command.IsMain, cancellationToken);

        await _repo.SaveChangesAsync(cancellationToken);

        return Result<EstablishmentDto>.Success(
            GetEstablishmentsByBranchQueryHandler.ToDto(entity)
        );
    }

    private Task LogMainChangeAsync(
        Guid companyId,
        Guid establishmentId,
        bool oldValue,
        bool newValue,
        CancellationToken ct
    ) =>
        _changeLogger.LogAsync(
            new ConfigurationChangeLogEntry(
                TenantId: _currentTenant.TenantId,
                CompanyId: companyId,
                Scope: OrgScope.Company,
                ScopeId: companyId,
                Key: null,
                EntityType: "Establishment",
                EntityId: establishmentId,
                FieldName: "IsMain",
                OldValue: oldValue ? "true" : "false",
                NewValue: newValue ? "true" : "false",
                ValueType: ConfigurationChangeValueType.Bool,
                ChangedBy: _user.UserId,
                Source: ConfigurationChangeSource.Api
            ),
            ct
        );
}
