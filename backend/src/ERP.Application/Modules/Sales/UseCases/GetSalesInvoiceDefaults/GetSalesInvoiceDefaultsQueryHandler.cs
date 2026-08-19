using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases.GetSalesInvoiceDefaults;

public sealed class GetSalesInvoiceDefaultsQueryHandler
    : IRequestHandler<GetSalesInvoiceDefaultsQuery, Result<SalesInvoiceDefaultsDto>>
{
    private const string SourceBranchSetting = "BranchSetting";
    private const string SourceBranchMainWarehouse = "BranchMainWarehouse";
    private const string SourceNone = "None";

    private readonly IOrgSettingsRepository _orgRepo;
    private readonly IEmissionPointRepository _epRepo;
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentBranch _currentBranch;

    public GetSalesInvoiceDefaultsQueryHandler(
        IOrgSettingsRepository orgRepo,
        IEmissionPointRepository epRepo,
        IWarehouseRepository warehouseRepo,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        ICurrentBranch currentBranch
    )
    {
        _orgRepo = orgRepo;
        _epRepo = epRepo;
        _warehouseRepo = warehouseRepo;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _currentBranch = currentBranch;
    }

    public async Task<Result<SalesInvoiceDefaultsDto>> Handle(
        GetSalesInvoiceDefaultsQuery query,
        CancellationToken cancellationToken
    )
    {
        var tenantId = _currentTenant.TenantId;
        var companyId = _currentCompany.CompanyId;

        // Leer configuración de empresa: DocTypeCode, PaymentMethodCode, PaymentTermId
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
        var emissionPointId = defaultEp?.Id;

        // DefaultWarehouseId: propietario Sucursal (OrgScope.Branch). CONFIG-FOUNDATION-P0-01 —
        // resuelto server-side (Branch OrgSetting → Warehouse.IsMain de la sucursal → null).
        // El frontend NUNCA debe sustituir un null aquí por "la primera bodega del listado".
        var (
            warehouseId,
            warehouseSource,
            requiresManualWarehouseSelection,
            warnings
        ) = await ResolveDefaultWarehouseAsync(tenantId, companyId, cancellationToken);

        return Result<SalesInvoiceDefaultsDto>.Success(
            new SalesInvoiceDefaultsDto(
                DefaultDocTypeCode: docTypeCode,
                DefaultSriPaymentMethodCode: payMethodCode,
                DefaultEmissionPointId: emissionPointId,
                DefaultWarehouseId: warehouseId,
                DefaultPaymentTermId: paymentTermId,
                FallbackDocTypeCode: SriSettings.FallbackDocTypeCode,
                FallbackSriPaymentMethodCode: SriSettings.FallbackSriPaymentMethodCode,
                DefaultWarehouseSource: warehouseSource,
                RequiresManualWarehouseSelection: requiresManualWarehouseSelection,
                ConfigurationWarnings: warnings
            )
        );
    }

    /// <summary>
    /// Precedencia P0 (Fase 3/4 de docs/architecture/configuration-engine-target-architecture.md):
    /// Branch OrgSetting (<c>invoice.default_warehouse_id</c>) → Warehouse.IsMain de la sucursal
    /// activa → null + selección manual obligatoria. La participación de CashRegister
    /// (precedencia mayor) queda documentada como aclaración pendiente — hoy se resuelve en
    /// frontend a partir de la sesión de caja activa, fuera de esta query; no se improvisa aquí
    /// sin contexto de caja disponible en este handler.
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
    )> ResolveDefaultWarehouseAsync(Guid tenantId, Guid companyId, CancellationToken ct)
    {
        if (!_currentBranch.HasBranchContext)
            return (null, SourceNone, true, Array.Empty<string>());

        var branchId = _currentBranch.BranchId;

        var branchSetting = await _orgRepo.GetAsync(
            tenantId,
            companyId,
            OrgScope.Branch,
            branchId,
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
                && configuredWarehouse.BranchId == branchId;

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

        var mainWarehouse = await _warehouseRepo.GetMainForBranchAsync(tenantId, branchId, ct);
        if (mainWarehouse is not null)
            return (mainWarehouse.Id, SourceBranchMainWarehouse, false, Array.Empty<string>());

        return (null, SourceNone, true, Array.Empty<string>());
    }
}
