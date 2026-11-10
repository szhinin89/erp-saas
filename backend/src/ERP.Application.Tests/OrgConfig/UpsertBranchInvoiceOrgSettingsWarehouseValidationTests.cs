using ERP.Application.Common;
using ERP.Application.Modules.Branches;
using ERP.Application.Modules.OrgConfig.DTOs;
using ERP.Application.Modules.OrgConfig.UseCases.GetBranchInvoiceOrgSettings;
using ERP.Application.Modules.OrgConfig.UseCases.UpsertBranchInvoiceOrgSettings;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.OrgConfig;

/// <summary>
/// BRANCH-CONFIG-WAREHOUSE-UNSET-01: la bodega por defecto de sucursal se valida al guardar.
/// Inexistente / de otra empresa (el repo con CompanyId no la devuelve), inactiva o de otra
/// sucursal → 422 sin escritura. null → se guarda (sin configurar, fallback a la principal).
/// BRANCH-CONFIG-CROSS-BRANCH-ACCESS-01: la sucursal de la ruta se autoriza con
/// IBranchAccessGuard.RequireBranchAsync; su rechazo se propaga con el mismo código y no hay
/// lectura ni escritura de org_settings.
/// </summary>
public sealed class UpsertBranchInvoiceOrgSettingsWarehouseValidationTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();

    private readonly Mock<IOrgSettingsRepository> _settings = new();
    private readonly Mock<IBranchAccessGuard> _branchAccess = new();
    private readonly Mock<IWarehouseRepository> _warehouses = new();
    private readonly List<OrgSetting> _written = new();

    public UpsertBranchInvoiceOrgSettingsWarehouseValidationTests()
    {
        _branchAccess
            .Setup(g => g.RequireBranchAsync(BranchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<BranchAccessContext>.Success(
                    new BranchAccessContext(UserId, TenantId, CompanyId, BranchId, "Matriz", true)
                )
            );
        _settings
            .Setup(r => r.UpsertAsync(It.IsAny<OrgSetting>(), It.IsAny<CancellationToken>()))
            .Callback<OrgSetting, CancellationToken>((s, _) => _written.Add(s))
            .Returns(Task.CompletedTask);
    }

    private static Warehouse NewWarehouse(Guid branchId, bool active = true)
    {
        var w = Warehouse.Create(
            TenantId,
            branchId,
            "Bodega",
            $"B{Guid.NewGuid():N}"[..8],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            UserId,
            CompanyId
        );
        if (!active)
            w.Disable(UserId);
        return w;
    }

    private void SeedWarehouse(Warehouse? warehouse, Guid id) =>
        _warehouses
            .Setup(r => r.GetByIdForCompanyAsync(TenantId, CompanyId, id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(warehouse);

    private void RejectBranch(Result<BranchAccessContext> rejection) =>
        _branchAccess
            .Setup(g => g.RequireBranchAsync(BranchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rejection);

    private Task<Result<BranchInvoiceOrgSettingsDto>> SendAsync(Guid? warehouseId)
    {
        var handler = new UpsertBranchInvoiceOrgSettingsCommandHandler(
            _settings.Object,
            _branchAccess.Object,
            _warehouses.Object
        );
        return handler.Handle(
            new UpsertBranchInvoiceOrgSettingsCommand(BranchId, warehouseId),
            CancellationToken.None
        );
    }

    private Task<Result<BranchInvoiceOrgSettingsDto>> ReadAsync() =>
        new GetBranchInvoiceOrgSettingsQueryHandler(_settings.Object, _branchAccess.Object).Handle(
            new GetBranchInvoiceOrgSettingsQuery(BranchId),
            CancellationToken.None
        );

    private void AssertRejectedWithoutWrite(Result<BranchInvoiceOrgSettingsDto> result)
    {
        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        result.Error.Should().Be(UpsertBranchInvoiceOrgSettingsCommandHandler.InvalidWarehouseMessage);
        _written.Should().BeEmpty();
        _settings.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Bodega_inexistente_o_de_otra_empresa_se_rechaza_sin_escribir()
    {
        var id = Guid.NewGuid();
        SeedWarehouse(null, id);

        AssertRejectedWithoutWrite(await SendAsync(id));
    }

    [Fact]
    public async Task Bodega_inactiva_se_rechaza_sin_escribir()
    {
        var warehouse = NewWarehouse(BranchId, active: false);
        SeedWarehouse(warehouse, warehouse.Id);

        AssertRejectedWithoutWrite(await SendAsync(warehouse.Id));
    }

    [Fact]
    public async Task Bodega_de_otra_sucursal_se_rechaza_sin_escribir()
    {
        var warehouse = NewWarehouse(Guid.NewGuid());
        SeedWarehouse(warehouse, warehouse.Id);

        AssertRejectedWithoutWrite(await SendAsync(warehouse.Id));
    }

    [Fact]
    public async Task Bodega_activa_de_la_sucursal_se_guarda()
    {
        var warehouse = NewWarehouse(BranchId);
        SeedWarehouse(warehouse, warehouse.Id);

        var result = await SendAsync(warehouse.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.DefaultWarehouseId.Should().Be(warehouse.Id);
        _written.Should().ContainSingle();
        _written[0].TenantId.Should().Be(TenantId);
        _written[0].CompanyId.Should().Be(CompanyId);
        _written[0].Key.Should().Be(OrgSettingKeys.Invoice.DefaultWarehouseId);
        _written[0].ScopeId.Should().Be(BranchId);
        _written[0].Value.Should().Be(warehouse.Id.ToString());
    }

    [Fact]
    public async Task Null_se_guarda_como_sin_configurar_sin_consultar_bodegas()
    {
        var result = await SendAsync(null);

        result.IsSuccess.Should().BeTrue();
        result.Value!.DefaultWarehouseId.Should().BeNull();
        _written.Should().ContainSingle();
        _written[0].Value.Should().BeNull();
        _warehouses.VerifyNoOtherCalls();
    }

    public static TheoryData<string> GuardRejectionCodes =>
        new()
        {
            ApiResponseCodes.Common.NotFound,
            ApiResponseCodes.Common.Forbidden,
            ApiResponseCodes.Common.CompanyScopeForbidden,
        };

    [Theory]
    [MemberData(nameof(GuardRejectionCodes))]
    public async Task Sucursal_no_autorizada_rechaza_el_guardado_con_el_codigo_del_guard(string code)
    {
        RejectBranch(Result<BranchAccessContext>.Failure("rechazo", code));

        var result = await SendAsync(null);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(code);
        _written.Should().BeEmpty();
        _warehouses.VerifyNoOtherCalls();
        _settings.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [MemberData(nameof(GuardRejectionCodes))]
    public async Task Sucursal_no_autorizada_rechaza_la_lectura_sin_consultar_org_settings(string code)
    {
        RejectBranch(Result<BranchAccessContext>.Failure("rechazo", code));

        var result = await ReadAsync();

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(code);
        _settings.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Sucursal_autorizada_lee_solo_su_scope()
    {
        _settings
            .Setup(r =>
                r.GetAllForScopeAsync(
                    TenantId,
                    CompanyId,
                    OrgScope.Branch,
                    BranchId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Array.Empty<OrgSetting>());

        var result = await ReadAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value!.DefaultWarehouseId.Should().BeNull();
    }
}
