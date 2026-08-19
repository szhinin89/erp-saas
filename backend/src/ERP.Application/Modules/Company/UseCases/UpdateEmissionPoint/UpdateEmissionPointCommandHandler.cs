using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;
using ERP.Application.Modules.Company.UseCases.GetEmissionPoints;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.UpdateEmissionPoint;

public sealed class UpdateEmissionPointCommandHandler
    : IRequestHandler<UpdateEmissionPointCommand, Result<EmissionPointDto>>
{
    private readonly IEmissionPointRepository _repo;
    private readonly IConfigurationChangeLogger _changeLogger;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _user;

    public UpdateEmissionPointCommandHandler(
        IEmissionPointRepository repo,
        IConfigurationChangeLogger changeLogger,
        ICurrentTenant currentTenant,
        ICurrentUser user
    )
    {
        _repo = repo;
        _changeLogger = changeLogger;
        _currentTenant = currentTenant;
        _user = user;
    }

    public async Task<Result<EmissionPointDto>> Handle(
        UpdateEmissionPointCommand command,
        CancellationToken cancellationToken
    )
    {
        var tenantId = _currentTenant.TenantId;
        var entity = await _repo.GetByIdAsync(command.Id, tenantId, cancellationToken);
        if (entity is null)
            return Result<EmissionPointDto>.Failure("Punto de emisión no encontrado.");

        var wasDefault = entity.IsDefault;

        if (command.IsDefault && !entity.IsDefault)
        {
            var clearedIds = await _repo.ClearDefaultExceptAsync(
                tenantId,
                entity.EstablishmentId,
                command.Id,
                _user.UserId,
                cancellationToken
            );
            foreach (var clearedId in clearedIds)
                await LogFlagChangeAsync(entity, clearedId, true, false, cancellationToken);
        }

        entity.Update(command.Name, command.EmissionType, _user.UserId);
        entity.SetDefault(command.IsDefault, _user.UserId);

        if (wasDefault != command.IsDefault)
            await LogFlagChangeAsync(entity, entity.Id, wasDefault, command.IsDefault, cancellationToken);

        await _repo.SaveChangesAsync(cancellationToken);

        return Result<EmissionPointDto>.Success(
            GetEmissionPointsByEstablishmentQueryHandler.ToDto(entity)
        );
    }

    // CONFIG-FOUNDATION-P2-01: EntityId puede ser un punto de emisión distinto de `entity` (el
    // que se estaba editando) cuando se registra el lado "desmarcado" del flip.
    private Task LogFlagChangeAsync(
        ERP.Domain.Modules.Company.Entities.EmissionPoint entity,
        Guid entityId,
        bool oldValue,
        bool newValue,
        CancellationToken ct
    ) =>
        _changeLogger.LogAsync(
            new ConfigurationChangeLogEntry(
                TenantId: entity.TenantId,
                CompanyId: entity.CompanyId,
                Scope: OrgScope.Establishment,
                ScopeId: entity.EstablishmentId,
                Key: null,
                EntityType: "EmissionPoint",
                EntityId: entityId,
                FieldName: "IsDefault",
                OldValue: oldValue ? "true" : "false",
                NewValue: newValue ? "true" : "false",
                ValueType: ConfigurationChangeValueType.Bool,
                ChangedBy: _user.UserId,
                Source: ConfigurationChangeSource.Api
            ),
            ct
        );
}
