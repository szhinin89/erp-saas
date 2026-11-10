using ERP.Application.Common;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Interfaces;

namespace ERP.Infrastructure.Services;

/// <summary>
/// SALES-PREFERENCES-INTEGRITY-01: única implementación de <see cref="IPriceListDefaultAssigner"/>.
/// Comparte el DbContext del llamador (vía <see cref="IPriceListRepository"/> e
/// <see cref="IConfigurationChangeLogger"/>) y exige una transacción abierta por el llamador
/// (<see cref="IUnitOfWork.ExecuteInTransactionAsync"/>): <c>uq_price_lists_tenant_company_default</c>
/// no es diferible y EF Core no garantiza el orden de los UPDATE, así que la lista anterior se
/// desmarca y se envía a la BD (flush dentro de la transacción) antes de marcar la nueva. El commit
/// —o el rollback completo— lo decide el llamador junto con el resto de su operación.
/// </summary>
public sealed class PriceListDefaultAssigner : IPriceListDefaultAssigner
{
    private readonly IPriceListRepository _repo;
    private readonly IConfigurationChangeLogger _changeLogger;
    private readonly IUnitOfWork _unitOfWork;

    public PriceListDefaultAssigner(
        IPriceListRepository repo,
        IConfigurationChangeLogger changeLogger,
        IUnitOfWork unitOfWork
    )
    {
        _repo = repo;
        _changeLogger = changeLogger;
        _unitOfWork = unitOfWork;
    }

    public async Task<PriceListDefaultAssignment> AssignAsync(
        Guid tenantId,
        Guid? priceListId,
        Guid changedBy,
        CancellationToken ct = default
    )
    {
        if (!_unitOfWork.HasActiveTransaction)
            throw new InvalidOperationException(
                "La reasignación de la lista de precios predeterminada debe ejecutarse dentro de una transacción (IUnitOfWork.ExecuteInTransactionAsync)."
            );

        PriceList? target = null;
        if (priceListId is { } id)
        {
            target = await _repo.GetByIdAsync(tenantId, id, ct);
            if (target is null)
                return new(PriceListDefaultAssignmentError.NotFound, null, false);
            if (!target.IsActive)
                return new(PriceListDefaultAssignmentError.Inactive, null, false);
        }

        var changed = false;
        var holders = await _repo.GetDefaultFlagHoldersAsync(tenantId, ct);
        foreach (var holder in holders.Where(h => h.Id != target?.Id))
        {
            holder.SetDefault(false, changedBy);
            await LogFlagChangeAsync(holder, oldValue: true, changedBy, ct);
            changed = true;
        }

        if (target is not null && !target.IsDefault)
        {
            // Flush intermedio (misma transacción): libera el índice único antes de marcar.
            if (changed)
                await _unitOfWork.SaveChangesAsync(ct);
            target.SetDefault(true, changedBy);
            await LogFlagChangeAsync(target, oldValue: false, changedBy, ct);
            changed = true;
        }

        return new(PriceListDefaultAssignmentError.None, target, changed);
    }

    private Task LogFlagChangeAsync(
        PriceList priceList,
        bool oldValue,
        Guid changedBy,
        CancellationToken ct
    ) =>
        _changeLogger.LogAsync(
            new ConfigurationChangeLogEntry(
                TenantId: priceList.TenantId,
                CompanyId: priceList.CompanyId,
                Scope: OrgScope.Company,
                ScopeId: priceList.CompanyId,
                Key: null,
                EntityType: "PriceList",
                EntityId: priceList.Id,
                FieldName: "IsDefault",
                OldValue: oldValue ? "true" : "false",
                NewValue: oldValue ? "false" : "true",
                ValueType: ConfigurationChangeValueType.Bool,
                ChangedBy: changedBy,
                Source: ConfigurationChangeSource.Api
            ),
            ct
        );
}
