using ERP.Application.Common;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Configuration;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence.Configuration;

/// <summary>
/// CONFIG-FOUNDATION-P1-03 — suite de integración (PostgreSQL 16 real vía Testcontainers) para el
/// guardrail de escritura de OrgSettingsRepository.UpsertAsync: a partir de esta entrega,
/// org_settings no acepta keys libres — toda escritura se valida contra
/// ConfigurationDefinitionCatalog. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class OrgSettingsRepositoryConfigurationGuardrailTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_org_settings_guardrail_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _createdBy;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        _createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _createdBy);
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "Test S.A.",
            createdBy: _createdBy
        );

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );
    }

    private OrgSetting BuildSetting(
        string key,
        string? value,
        SettingDataType dataType,
        OrgScope scope,
        Guid scopeId
    ) => OrgSetting.Create(_tenantId, _companyId, scope, scopeId, key, value, dataType, _createdBy);

    [Fact]
    public async Task Key_desconocida_se_rechaza_sin_persistir_nada()
    {
        await using var db = CreateContext();
        var repo = new OrgSettingsRepository(db);
        var setting = BuildSetting(
            "no.existe.esta.key",
            "x",
            SettingDataType.String,
            OrgScope.Company,
            _companyId
        );

        var act = async () => await repo.UpsertAsync(setting);

        await act.Should()
            .ThrowAsync<ConfigurationDefinitionViolationException>()
            .Where(e => e.Code == "configuration_key_unknown");

        (await CountRowsAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("ride.branding.primary_color_hex")]
    [InlineData("ride.branding.logo_storage_path")]
    [InlineData("decimal.quantity")]
    [InlineData("decimal.sales.unitPrice")]
    public async Task Keys_legacy_eliminadas_se_rechazan_como_desconocidas(string legacyKey)
    {
        await using var db = CreateContext();
        var repo = new OrgSettingsRepository(db);
        var setting = BuildSetting(
            legacyKey,
            "x",
            SettingDataType.String,
            OrgScope.Company,
            _companyId
        );

        var act = async () => await repo.UpsertAsync(setting);

        await act.Should()
            .ThrowAsync<ConfigurationDefinitionViolationException>()
            .Where(e => e.Code == "configuration_key_unknown");
    }

    [Fact]
    public async Task Scope_no_permitido_se_rechaza()
    {
        await using var db = CreateContext();
        var repo = new OrgSettingsRepository(db);
        // invoice.default_warehouse_id solo permite Branch, no Company.
        var setting = BuildSetting(
            OrgSettingKeys.Invoice.DefaultWarehouseId,
            Guid.NewGuid().ToString(),
            SettingDataType.Guid,
            OrgScope.Company,
            _companyId
        );

        var act = async () => await repo.UpsertAsync(setting);

        await act.Should()
            .ThrowAsync<ConfigurationDefinitionViolationException>()
            .Where(e => e.Code == "configuration_scope_not_allowed");
    }

    [Fact]
    public async Task DataType_incorrecto_se_rechaza()
    {
        await using var db = CreateContext();
        var repo = new OrgSettingsRepository(db);
        // sales.consumer_final.max_amount es Decimal, no String.
        var setting = BuildSetting(
            OrgSettingKeys.Sales.ConsumerFinalMaxAmount,
            "100.00",
            SettingDataType.String,
            OrgScope.Company,
            _companyId
        );

        var act = async () => await repo.UpsertAsync(setting);

        await act.Should()
            .ThrowAsync<ConfigurationDefinitionViolationException>()
            .Where(e => e.Code == "configuration_data_type_mismatch");
    }

    [Fact]
    public async Task Presentation_decimal_fuera_de_rango_se_rechaza()
    {
        await using var db = CreateContext();
        var repo = new OrgSettingsRepository(db);
        var setting = BuildSetting(
            OrgSettingKeys.Presentation.DecimalQuantity,
            "99",
            SettingDataType.Int,
            OrgScope.Company,
            _companyId
        );

        var act = async () => await repo.UpsertAsync(setting);

        await act.Should()
            .ThrowAsync<ConfigurationDefinitionViolationException>()
            .Where(e => e.Code == "configuration_value_invalid");
    }

    [Fact]
    public async Task Company_branding_color_invalido_se_rechaza()
    {
        await using var db = CreateContext();
        var repo = new OrgSettingsRepository(db);
        var setting = BuildSetting(
            OrgSettingKeys.CompanyBranding.PrimaryColor,
            "not-a-color",
            SettingDataType.String,
            OrgScope.Company,
            _companyId
        );

        var act = async () => await repo.UpsertAsync(setting);

        await act.Should()
            .ThrowAsync<ConfigurationDefinitionViolationException>()
            .Where(e => e.Code == "configuration_value_invalid");
    }

    [Fact]
    public async Task Company_branding_color_null_o_vacio_es_valido_ausencia_de_configuracion()
    {
        await using var db = CreateContext();
        var repo = new OrgSettingsRepository(db);
        var setting = BuildSetting(
            OrgSettingKeys.CompanyBranding.PrimaryColor,
            null,
            SettingDataType.String,
            OrgScope.Company,
            _companyId
        );

        await repo.UpsertAsync(setting);
        await repo.SaveChangesAsync();

        (await CountRowsAsync()).Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(ValidWritesPerSection))]
    public async Task Escritura_valida_de_cada_seccion_actual_sigue_funcionando(
        string key,
        string value,
        SettingDataType dataType,
        OrgScope scope
    )
    {
        await using var db = CreateContext();
        var repo = new OrgSettingsRepository(db);
        var scopeId = scope == OrgScope.Branch ? Guid.NewGuid() : _companyId;
        var setting = BuildSetting(key, value, dataType, scope, scopeId);

        await repo.UpsertAsync(setting);
        await repo.SaveChangesAsync();

        var persisted = await repo.GetAsync(_tenantId, _companyId, scope, scopeId, key);
        persisted.Should().NotBeNull();
        persisted!.Value.Should().Be(value);
    }

    public static IEnumerable<object[]> ValidWritesPerSection()
    {
        // Ventas — defaults de factura (Company e Invoice.DefaultWarehouseId a nivel Branch).
        yield return new object[]
        {
            OrgSettingKeys.Invoice.DefaultDocTypeCode,
            "01",
            SettingDataType.String,
            OrgScope.Company,
        };
        yield return new object[]
        {
            OrgSettingKeys.Invoice.DefaultWarehouseId,
            Guid.NewGuid().ToString(),
            SettingDataType.Guid,
            OrgScope.Branch,
        };

        // Fiscal / Consumidor Final.
        yield return new object[]
        {
            OrgSettingKeys.Sales.ConsumerFinalMaxAmount,
            "200.00",
            SettingDataType.Decimal,
            OrgScope.Company,
        };

        // Decimales de presentación.
        yield return new object[]
        {
            OrgSettingKeys.Presentation.DecimalQuantity,
            "4",
            SettingDataType.Int,
            OrgScope.Company,
        };

        // Marca de empresa.
        yield return new object[]
        {
            OrgSettingKeys.CompanyBranding.PrimaryColor,
            "#1E88E5",
            SettingDataType.String,
            OrgScope.Company,
        };
        yield return new object[]
        {
            OrgSettingKeys.CompanyBranding.Slogan,
            "Confianza y calidad",
            SettingDataType.String,
            OrgScope.Company,
        };
    }

    private async Task<int> CountRowsAsync()
    {
        await using var db = CreateContext();
        return await db.OrgSettings.IgnoreQueryFilters().CountAsync();
    }

    private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Guid companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => companyId != Guid.Empty;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }
}
