using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Geography.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Branches.UseCases.CreateBranch;

public sealed class CreateBranchCommandHandler
    : IRequestHandler<CreateBranchCommand, Result<BranchListItemDto>>
{
    private readonly IBranchRepository _repo;
    private readonly IGeographyReadRepository _geo;
    private readonly IUserActivityRepository _activity;
    private readonly IConfigurationChangeLogger _changeLogger;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentUser _user;

    public CreateBranchCommandHandler(
        IBranchRepository repo,
        IGeographyReadRepository geo,
        IUserActivityRepository activity,
        IConfigurationChangeLogger changeLogger,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentUser user
    )
    {
        _repo = repo;
        _geo = geo;
        _activity = activity;
        _changeLogger = changeLogger;
        _currentTenant = tenant;
        _company = company;
        _user = user;
    }

    public async Task<Result<BranchListItemDto>> Handle(
        CreateBranchCommand command,
        CancellationToken cancellationToken
    )
    {
        var locErr = await BranchLocationValidation.ValidateAsync(
            _geo,
            command.CountryId,
            command.ProvinceId,
            command.CantonId,
            command.ParishId,
            cancellationToken
        );
        if (locErr is not null)
            return Result<BranchListItemDto>.Failure(locErr);

        var tenantId = _currentTenant.TenantId;
        var userId = _user.UserId;

        var companyId = _company.CompanyId;
        IReadOnlyList<Guid> clearedIds = Array.Empty<Guid>();
        if (command.IsMainBranch)
            clearedIds = await _repo.ClearMainBranchExceptAsync(
                tenantId,
                companyId,
                null,
                userId,
                cancellationToken
            );

        var code = $"SUC-{DateTime.UtcNow.Year}-{Guid.NewGuid():N}"[..14];

        var entity = Branch.Create(
            tenantId,
            command.Name,
            command.Address,
            code,
            command.Description,
            command.Reference,
            command.PostalCode,
            command.Phone,
            command.SecondaryPhone,
            command.Email,
            command.Website,
            command.ManagerName,
            command.ManagerPosition,
            command.ManagerEmail,
            command.ManagerPhone,
            command.CountryId,
            command.ProvinceId,
            command.CantonId,
            command.ParishId,
            command.Latitude,
            command.Longitude,
            command.OpeningDate,
            command.InternalNotes,
            command.IsMainBranch,
            userId,
            companyId
        );

        if (!command.IsActive)
            entity.Disable(userId);

        await _repo.AddAsync(entity, cancellationToken);

        foreach (var clearedId in clearedIds)
            await LogMainBranchChangeAsync(companyId, clearedId, true, false, cancellationToken);
        if (command.IsMainBranch)
            await LogMainBranchChangeAsync(companyId, entity.Id, false, true, cancellationToken);

        await _activity.AddAsync(
            UserActivity.Create(
                tenantId,
                userId,
                _user.Email,
                _user.FullName,
                module: "branches",
                action: "branch.create",
                entityType: "Branch",
                entityId: entity.Id,
                description: entity.Name
            ),
            cancellationToken
        );
        await _repo.SaveChangesAsync(cancellationToken);

        return Result<BranchListItemDto>.Success(
            new BranchListItemDto(
                entity.Id,
                entity.Name,
                entity.Code,
                entity.Address,
                entity.CountryId,
                entity.ProvinceId,
                entity.CantonId,
                entity.ParishId,
                entity.Phone,
                entity.Email,
                entity.ManagerName,
                entity.IsActive,
                entity.IsMainBranch
            )
        );
    }

    // CONFIG-FOUNDATION-P2-01: mismo patrón que UpdateBranchCommandHandler.
    private Task LogMainBranchChangeAsync(
        Guid companyId,
        Guid branchId,
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
                EntityType: "Branch",
                EntityId: branchId,
                FieldName: "IsMainBranch",
                OldValue: oldValue ? "true" : "false",
                NewValue: newValue ? "true" : "false",
                ValueType: ConfigurationChangeValueType.Bool,
                ChangedBy: _user.UserId,
                Source: ConfigurationChangeSource.Api
            ),
            ct
        );
}
