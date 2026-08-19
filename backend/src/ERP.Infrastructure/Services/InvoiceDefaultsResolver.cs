using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Infrastructure.Services;

/// <summary>
/// CONFIG-FOUNDATION-P1-04: única implementación de <see cref="IInvoiceDefaultsResolver"/>.
/// Movido desde GetSalesInvoiceDefaultsQueryHandler (que hasta esta entrega leía
/// IOrgSettingsRepository directamente) — el handler ahora solo ensambla el DTO de salida.
/// </summary>
public sealed class InvoiceDefaultsResolver : IInvoiceDefaultsResolver
{
    private const string SourceBranchSetting = "BranchSetting";
    private const string SourceBranchMainWarehouse = "BranchMainWarehouse";
    private const string SourceNone = "None";

    private readonly IOrgSettingsRepository _orgRepo;
    private readonly IEmissionPointRepository _epRepo;
    private readonly IWarehouseRepository _warehouseRepo;

    public InvoiceDefaultsResolver(
        IOrgSettingsRepository orgRepo,
        IEmissionPointRepository epRepo,
        IWarehouseRepository warehouseRepo
    )
    {
        _orgRepo = orgRepo;
        _epRepo = epRepo;
        _warehouseRepo = warehouseRepo;
    }

    public async Task<InvoiceDefaultsResult> GetAsync(
        Guid tenantId,
        Guid companyId,
        Guid? branchId,
        CancellationToken cancellationToken = default
    )
    {
        var orgSettings = await _orgRepo.GetAllForScopeAsync(
            tenantId,
            companyId,
            OrgScope.Company,
            companyId,
            cancellationToken
        );
        var orgLookup = orgSettings.ToDictionary(s => s.Key, s => s.Value);

        string? Resolve(string key) =>
            orgLookup.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

        Guid? ResolveGuid(string key)
        {
            if (orgLookup.TryGetValue(key, out var v) && Guid.TryParse(v, out var g))
                return g;
            return null;
        }

        var docTypeCode = Resolve(OrgSettingKeys.Invoice.DefaultDocTypeCode);
        var payMethodCode = Resolve(OrgSettingKeys.Invoice.DefaultPaymentMethodCode);
        var paymentTermId = ResolveGuid(OrgSettingKeys.Invoice.DefaultPaymentTermId);

        // DefaultEmissionPointId: resuelto siempre desde EmissionPoint.IsDefault (única fuente).
        var defaultEp = await _epRepo.GetDefaultForCompanyAsync(
            tenantId,
            companyId,
            cancellationToken
        );

        var (warehouseId, warehouseSource, requiresManualWarehouseSelection, warnings) =
            await ResolveDefaultWarehouseAsync(tenantId, companyId, branchId, cancellationToken);

        return new InvoiceDefaultsResult(
            DefaultDocTypeCode: docTypeCode,
            DefaultSriPaymentMethodCode: payMethodCode,
            DefaultPaymentTermId: paymentTermId,
            DefaultEmissionPointId: defaultEp?.Id,
            DefaultWarehouseId: warehouseId,
            DefaultWarehouseSource: warehouseSource,
            RequiresManualWarehouseSelection: requiresManualWarehouseSelection,
            ConfigurationWarnings: warnings
        );
    }

    /// <summary>
    /// Precedencia P0 (Fase 3/4 de docs/architecture/configuration-engine-target-architecture.md):
    /// Branch OrgSetting (<c>invoice.default_warehouse_id</c>) → Warehouse.IsMain de la sucursal
    /// activa → null + selección manual obligatoria. La participación de CashRegister
    /// (precedencia mayor) queda documentada como aclaración pendiente — se resuelve en frontend
    /// a partir de la sesión de caja activa, fuera de este resolver.
    ///
    /// Fail-closed: si el OrgSetting de sucursal apunta a un valor no parseable como Guid, o a una
    /// bodega inexistente/inactiva/de otra sucursal o empresa, el valor se trata como corrupto —
    /// NUNCA cae en silencio a Warehouse.IsMain (eso disfrazaría un dato de configuración inválido
    /// como si fuera "no configurado"). Se bloquea la selección automática y se devuelve un
    /// warning explícito.
    /// </summary>
    private async Task<(
        Guid? WarehouseId,
        string Source,
        bool RequiresManualSelection,
        IReadOnlyList<string> Warnings
    )> ResolveDefaultWarehouseAsync(
        Guid tenantId,
        Guid companyId,
        Guid? branchId,
        CancellationToken ct
    )
    {
        if (branchId is not { } resolvedBranchId)
            return (null, SourceNone, true, Array.Empty<string>());

        var branchSetting = await _orgRepo.GetAsync(
            tenantId,
            companyId,
            OrgScope.Branch,
            resolvedBranchId,
            OrgSettingKeys.Invoice.DefaultWarehouseId,
            ct
        );

        if (branchSetting is not null && !string.IsNullOrWhiteSpace(branchSetting.Value))
        {
            if (!Guid.TryParse(branchSetting.Value, out var configuredWarehouseId))
            {
                return (
                    null,
                    SourceNone,
                    true,
                    new[]
                    {
                        "La bodega configurada para esta sucursal (invoice.default_warehouse_id) tiene un valor inválido. Corrija la configuración de la sucursal antes de continuar.",
                    }
                );
            }

            var configuredWarehouse = await _warehouseRepo.GetByIdAsync(
                tenantId,
                configuredWarehouseId,
                ct
            );

            var isValid =
                configuredWarehouse is not null
                && configuredWarehouse.IsActive
                && configuredWarehouse.BranchId == resolvedBranchId;

            if (!isValid)
            {
                return (
                    null,
                    SourceNone,
                    true,
                    new[]
                    {
                        "La bodega configurada para esta sucursal (invoice.default_warehouse_id) ya no existe, está inactiva o pertenece a otra sucursal. Corrija la configuración de la sucursal antes de continuar.",
                    }
                );
            }

            return (configuredWarehouse!.Id, SourceBranchSetting, false, Array.Empty<string>());
        }

        var mainWarehouse = await _warehouseRepo.GetMainForBranchAsync(
            tenantId,
            resolvedBranchId,
            ct
        );
        if (mainWarehouse is not null)
            return (mainWarehouse.Id, SourceBranchMainWarehouse, false, Array.Empty<string>());

        return (null, SourceNone, true, Array.Empty<string>());
    }
}
