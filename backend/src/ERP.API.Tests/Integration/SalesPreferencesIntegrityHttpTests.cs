using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ERP.API.Tests.Support;
using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Integration;

/// <summary>
/// SALES-PREFERENCES-INTEGRITY-01 — Configuración → Ventas contra API real y PostgreSQL
/// (Testcontainers). Usuario no admin con membresía solo en Empresa A y permisos
/// <c>erp.companies.update</c> + <c>pricing.update</c>. Empresa B tiene su propia lista de precios;
/// existe una condición de pago de otro tenant. El caso "sin permiso de Pricing" está en
/// <see cref="CompanySettingsIsolationHttpTests"/> (su usuario no tiene <c>pricing.update</c>).
/// </summary>
public sealed class SalesPreferencesIntegrityFixture : IAsyncLifetime
{
    public const string Role = "Supervisor";

    private readonly PostgreSqlTestWebAppFactory _factory = new(useHttpCompanyContext: true);
    private Guid _adminId;

    public Guid TenantId { get; private set; }
    public Guid CompanyAId { get; private set; }
    public Guid CompanyBId { get; private set; }
    public Guid BranchAId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid GeneralListId { get; private set; }
    public Guid MayoristaListId { get; private set; }
    public Guid ForeignListId { get; private set; }
    public Guid ActiveTermId { get; private set; }
    public Guid InactiveTermId { get; private set; }
    public Guid ForeignTenantTermId { get; private set; }

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("JWT__SECRETKEY", IntegrationTestConstants.JwtSecretKey);
        Environment.SetEnvironmentVariable("JWT__ISSUER", "ZHTechnologies");
        Environment.SetEnvironmentVariable("JWT__AUDIENCE", "ERPUsers");

        await _factory.InitializeAsync();
        await _factory.MigrateAsync();
        await SeedAsync();

        _factory.MutableTenant.TenantId = TenantId;
        _factory.MutableUser.UserId = UserId;
        _factory.MutableUser.Role = Role;
        JobCompanyContext.Current = Guid.Empty;
        JobBranchContext.Current = Guid.Empty;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    public HttpClient CreateClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtFactory.CreateSessionJwt(TenantId, UserId, role: Role)
        );
        client.DefaultRequestHeaders.Add("X-Company-Id", CompanyAId.ToString());
        client.DefaultRequestHeaders.Add("X-Branch-Id", BranchAId.ToString());
        return client;
    }

    public IServiceScope CreateScope() => _factory.Services.CreateScope();

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        _adminId = Guid.NewGuid();

        var tenant = Tenant.Create("ZH-SalesPreferences", $"zh-sp-{Guid.NewGuid():N}", _adminId);
        var otherTenant = Tenant.Create("ZH-SalesPreferences-Otro", $"zh-spo-{Guid.NewGuid():N}", _adminId);
        db.Tenants.AddRange(tenant, otherTenant);
        await db.SaveChangesAsync();
        TenantId = tenant.Id;

        var companyA = Company.CreateManaged(
            TenantId,
            taxIdentificationNumber: $"179{Guid.NewGuid():N}"[..13],
            legalName: "Empresa A S.A.",
            createdBy: _adminId
        );
        var companyB = Company.CreateManaged(
            TenantId,
            taxIdentificationNumber: $"179{Guid.NewGuid():N}"[..13],
            legalName: "Empresa B S.A.",
            createdBy: _adminId
        );
        db.Companies.AddRange(companyA, companyB);
        await db.SaveChangesAsync();
        CompanyAId = companyA.Id;
        CompanyBId = companyB.Id;

        db.CompanyPrecisionPolicies.Add(
            ERP.Domain.Configuration.Entities.CompanyPrecisionPolicy.CreateStandardCommercial(
                TenantId,
                CompanyAId,
                _adminId
            )
        );

        var branchA = Branch.Create(
            TenantId,
            "Matriz A",
            "Av. Principal 123",
            "SUC-A",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            isMainBranch: true,
            _adminId,
            companyId: CompanyAId
        );
        db.Branches.Add(branchA);

        var general = PriceList.Create(TenantId, CompanyAId, "GENERAL", "General", "USD", true, _adminId);
        var mayorista = PriceList.Create(TenantId, CompanyAId, "MAYORISTA", "Mayorista", "USD", false, _adminId);
        var foreign = PriceList.Create(TenantId, CompanyBId, "GENERAL-B", "General B", "USD", true, _adminId);
        db.PriceLists.AddRange(general, mayorista, foreign);

        var activeTerm = PaymentTerm.Create(TenantId, "CONT", "Contado", 1, 0, _adminId);
        var inactiveTerm = PaymentTerm.Create(TenantId, "C30", "30 días", 1, 30, _adminId);
        inactiveTerm.Disable(_adminId);
        var foreignTerm = PaymentTerm.Create(otherTenant.Id, "CONT", "Contado otro tenant", 1, 0, _adminId);
        db.PaymentTerms.AddRange(activeTerm, inactiveTerm, foreignTerm);
        await db.SaveChangesAsync();

        BranchAId = branchA.Id;
        GeneralListId = general.Id;
        MayoristaListId = mayorista.Id;
        ForeignListId = foreign.Id;
        ActiveTermId = activeTerm.Id;
        InactiveTermId = inactiveTerm.Id;
        ForeignTenantTermId = foreignTerm.Id;

        var profile = AccessProfile.Create(TenantId, CompanyAId, "Preferencias de ventas A", null, _adminId);
        db.AccessProfiles.Add(profile);
        await db.SaveChangesAsync();
        foreach (
            var key in new[]
            {
                "erp.companies.view",
                "erp.companies.update",
                "settings.company.view",
                "pricing.view",
                "pricing.create",
                "pricing.update",
            }
        )
            db.AccessProfilePermissions.Add(
                AccessProfilePermission.Create(TenantId, profile.Id, key, true, _adminId)
            );

        var user = IdentityUser.Create(
            $"usuario-{Guid.NewGuid():N}",
            "Usuario",
            "Ventas",
            $"usuario-{Guid.NewGuid():N}@example.com",
            "hash",
            _adminId
        );
        db.IdentityUsers.Add(user);
        await db.SaveChangesAsync();
        UserId = user.Id;

        var membership = CompanyUserMembership.Create(CompanyAId, user.Id, Role, profile.Id, _adminId);
        db.CompanyUserMemberships.Add(membership);
        await db.SaveChangesAsync();
        db.CompanyUserBranches.Add(
            CompanyUserBranch.Create(TenantId, CompanyAId, membership.Id, BranchAId, _adminId)
        );
        await db.SaveChangesAsync();
    }
}

[Trait("Category", "PostgreSql")]
public sealed class SalesPreferencesIntegrityHttpTests : IClassFixture<SalesPreferencesIntegrityFixture>
{
    private const string Defaults = "/api/v1/org-config/company/invoice-defaults";
    private const string SalesInvoiceDefaults = "/api/v1/sales/invoice-defaults";

    private readonly SalesPreferencesIntegrityFixture _f;

    public SalesPreferencesIntegrityHttpTests(SalesPreferencesIntegrityFixture fixture) => _f = fixture;

    private sealed record Snapshot(
        IReadOnlyDictionary<string, string?> Settings,
        IReadOnlyDictionary<Guid, (bool IsDefault, bool IsActive)> Lists,
        int ChangeLogCount
    );

    /// <summary>Estado base: GENERAL activa y predeterminada, MAYORISTA activa, sin preferencias.</summary>
    private async Task ResetAsync()
    {
        using (var scope = _f.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var lists = await db.PriceLists.IgnoreQueryFilters()
                .Where(p => p.CompanyId == _f.CompanyAId)
                .ToListAsync();
            foreach (var list in lists)
            {
                if (!list.IsActive)
                    list.Enable(Guid.Empty);
                list.SetDefault(false, Guid.Empty);
            }
            await db.SaveChangesAsync();
            lists.Single(p => p.Id == _f.GeneralListId).SetDefault(true, Guid.Empty);
            await db.SaveChangesAsync();
        }

        using var client = _f.CreateClient();
        var reset = await client.PutAsJsonAsync(
            Defaults,
            new { defaultDocTypeCode = (string?)null, defaultSriPaymentMethodCode = (string?)null }
        );
        reset.StatusCode.Should().Be(HttpStatusCode.OK, await reset.Content.ReadAsStringAsync());
    }

    private async Task<Snapshot> LoadAsync()
    {
        using var scope = _f.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var settings = await db.OrgSettings.IgnoreQueryFilters()
            .Where(s => s.CompanyId == _f.CompanyAId && s.Scope == OrgScope.Company)
            .ToDictionaryAsync(s => s.Key, s => s.Value);
        var lists = await db.PriceLists.IgnoreQueryFilters()
            .Where(p => p.TenantId == _f.TenantId)
            .ToDictionaryAsync(p => p.Id, p => (p.IsDefault, p.IsActive));
        var changeLogCount = await db.ConfigurationChangeLogs.IgnoreQueryFilters()
            .CountAsync(l => l.TenantId == _f.TenantId);
        return new Snapshot(settings, lists, changeLogCount);
    }

    private async Task DisableGeneralKeepingFlagAsync()
    {
        using var scope = _f.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var general = await db.PriceLists.IgnoreQueryFilters().SingleAsync(p => p.Id == _f.GeneralListId);
        general.Disable(Guid.Empty); // conserva IsDefault (dato heredado)
        await db.SaveChangesAsync();
    }

    private async Task<int> PriceListFlagLogsAsync(Guid priceListId)
    {
        using var scope = _f.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        return await db.ConfigurationChangeLogs.IgnoreQueryFilters()
            .CountAsync(l => l.EntityType == "PriceList" && l.EntityId == priceListId && l.FieldName == "IsDefault");
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement.GetProperty("data").Clone();
    }

    [Fact]
    public async Task Guardado_unificado_reasigna_y_quita_la_lista_en_una_sola_operacion()
    {
        await ResetAsync();
        using var client = _f.CreateClient();

        var saved = await DataAsync(
            await client.PutAsJsonAsync(
                Defaults,
                new
                {
                    defaultDocTypeCode = "01",
                    defaultSriPaymentMethodCode = "20",
                    defaultPaymentTermId = _f.ActiveTermId,
                    defaultPriceList = new { priceListId = _f.MayoristaListId },
                }
            )
        );
        saved.GetProperty("defaultPriceListId").GetGuid().Should().Be(_f.MayoristaListId);

        var read = await DataAsync(await client.GetAsync(Defaults));
        read.GetProperty("defaultDocTypeCode").GetString().Should().Be("01");
        read.GetProperty("defaultSriPaymentMethodCode").GetString().Should().Be("20");
        read.GetProperty("defaultPaymentTermId").GetGuid().Should().Be(_f.ActiveTermId);
        read.GetProperty("defaultPriceListId").GetGuid().Should().Be(_f.MayoristaListId);
        read.GetProperty("configurationWarnings").GetArrayLength().Should().Be(0);
        read.GetProperty("allowedDocTypeCodes").EnumerateArray().Select(c => c.GetString())
            .Should().Equal("01");

        var afterFlip = await LoadAsync();
        afterFlip.Lists[_f.GeneralListId].IsDefault.Should().BeFalse();
        afterFlip.Lists[_f.MayoristaListId].IsDefault.Should().BeTrue();
        afterFlip.Lists[_f.ForeignListId].IsDefault.Should().BeTrue("la lista de B no se toca");

        // Quitar la lista predeterminada: sin lista, Ventas aplica el PVP.
        var cleared = await DataAsync(
            await client.PutAsJsonAsync(
                Defaults,
                new
                {
                    defaultDocTypeCode = "01",
                    defaultSriPaymentMethodCode = "20",
                    defaultPaymentTermId = _f.ActiveTermId,
                    defaultPriceList = new { priceListId = (Guid?)null },
                }
            )
        );
        cleared.GetProperty("defaultPriceListId").ValueKind.Should().Be(JsonValueKind.Null);
        var afterClear = await LoadAsync();
        afterClear.Lists.Where(l => l.Key != _f.ForeignListId).Should().OnlyContain(l => !l.Value.IsDefault);
    }

    [Fact]
    public async Task Vacios_se_guardan_como_null()
    {
        await ResetAsync();
        using var client = _f.CreateClient();

        var saved = await DataAsync(
            await client.PutAsJsonAsync(
                Defaults,
                new { defaultDocTypeCode = "", defaultSriPaymentMethodCode = "  ", defaultPaymentTermId = (Guid?)null }
            )
        );

        saved.GetProperty("defaultDocTypeCode").ValueKind.Should().Be(JsonValueKind.Null);
        var state = await LoadAsync();
        state.Settings[OrgSettingKeys.Invoice.DefaultDocTypeCode].Should().BeNull();
        state.Settings[OrgSettingKeys.Invoice.DefaultPaymentMethodCode].Should().BeNull();
    }

    [Fact]
    public async Task Rechazos_no_persisten_ningun_cambio()
    {
        await ResetAsync();
        using var client = _f.CreateClient();
        var before = await LoadAsync();

        object Payload(string? docType = "01", string? method = "20", Guid? term = null, Guid? list = null) =>
            new
            {
                defaultDocTypeCode = docType,
                defaultSriPaymentMethodCode = method,
                defaultPaymentTermId = term,
                defaultPriceList = new { priceListId = list ?? _f.MayoristaListId },
            };

        (await client.PutAsJsonAsync(Defaults, Payload(docType: "07"))).StatusCode
            .Should().Be(HttpStatusCode.UnprocessableEntity, "solo Factura 01 es default de ventas");
        (await client.PutAsJsonAsync(Defaults, Payload(method: "ZZ"))).StatusCode
            .Should().Be(HttpStatusCode.UnprocessableEntity);
        (await client.PutAsJsonAsync(Defaults, Payload(term: _f.InactiveTermId))).StatusCode
            .Should().Be(HttpStatusCode.UnprocessableEntity);
        (await client.PutAsJsonAsync(Defaults, Payload(term: _f.ForeignTenantTermId))).StatusCode
            .Should().Be(HttpStatusCode.UnprocessableEntity);
        (await client.PutAsJsonAsync(Defaults, Payload(list: _f.ForeignListId))).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "la lista de B no es visible desde A");

        using (var scope = _f.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var mayorista = await db.PriceLists.IgnoreQueryFilters().SingleAsync(p => p.Id == _f.MayoristaListId);
            mayorista.Disable(Guid.Empty);
            await db.SaveChangesAsync();
        }
        (await client.PutAsJsonAsync(Defaults, Payload())).StatusCode
            .Should().Be(HttpStatusCode.UnprocessableEntity, "no se marca una lista deshabilitada");

        var after = await LoadAsync();
        after.Settings.Should().Equal(before.Settings);
        after.Lists.Where(l => l.Key != _f.MayoristaListId).Should()
            .Equal(before.Lists.Where(l => l.Key != _f.MayoristaListId));
        after.Lists[_f.MayoristaListId].IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task Lista_deshabilitada_que_conserva_la_bandera_se_advierte_y_no_bloquea_la_reasignacion()
    {
        await ResetAsync();
        await DisableGeneralKeepingFlagAsync();
        using var client = _f.CreateClient();

        var read = await DataAsync(await client.GetAsync(Defaults));
        read.GetProperty("defaultPriceListId").ValueKind.Should().Be(JsonValueKind.Null);
        read.GetProperty("configurationWarnings").GetArrayLength().Should().Be(1);

        // uq_price_lists_tenant_company_default no filtra por estado: sin desmarcar la deshabilitada
        // este flip violaría el índice en PostgreSQL.
        var flip = await client.PatchAsync($"/api/v1/pricing/price-lists/{_f.MayoristaListId}/set-default", null);
        flip.StatusCode.Should().Be(HttpStatusCode.OK, await flip.Content.ReadAsStringAsync());

        var state = await LoadAsync();
        state.Lists[_f.GeneralListId].IsDefault.Should().BeFalse();
        state.Lists[_f.MayoristaListId].IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Preferencias_antiguas_invalidas_se_conservan_y_Ventas_las_ignora_con_advertencia()
    {
        await ResetAsync();
        using (var scope = _f.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var settings = await db.OrgSettings.IgnoreQueryFilters()
                .Where(s => s.CompanyId == _f.CompanyAId && s.Scope == OrgScope.Company)
                .ToListAsync();
            settings.Single(s => s.Key == OrgSettingKeys.Invoice.DefaultDocTypeCode).UpdateValue("07", Guid.Empty);
            settings.Single(s => s.Key == OrgSettingKeys.Invoice.DefaultPaymentTermId)
                .UpdateValue(_f.InactiveTermId.ToString(), Guid.Empty);
            await db.SaveChangesAsync();
        }
        using var client = _f.CreateClient();

        var admin = await DataAsync(await client.GetAsync(Defaults));
        admin.GetProperty("defaultDocTypeCode").GetString().Should().Be("07");
        admin.GetProperty("defaultPaymentTermId").GetGuid().Should().Be(_f.InactiveTermId);
        admin.GetProperty("configurationWarnings").GetArrayLength().Should().Be(2);

        var sales = await DataAsync(await client.GetAsync(SalesInvoiceDefaults));
        sales.GetProperty("defaultDocTypeCode").ValueKind.Should().Be(JsonValueKind.Null);
        sales.GetProperty("defaultPaymentTermId").ValueKind.Should().Be(JsonValueKind.Null);
        sales.GetProperty("configurationWarnings").GetArrayLength().Should().BeGreaterThanOrEqualTo(2);

        var stored = await LoadAsync();
        stored.Settings[OrgSettingKeys.Invoice.DefaultDocTypeCode].Should().Be("07", "nunca se borra automáticamente");
    }

    [Fact]
    public async Task Create_predeterminada_con_lista_deshabilitada_que_conserva_la_bandera_no_viola_el_indice()
    {
        await ResetAsync();
        await DisableGeneralKeepingFlagAsync();
        using var client = _f.CreateClient();

        var created = await client.PostAsJsonAsync(
            "/api/v1/pricing/price-lists",
            new { code = $"N{Guid.NewGuid():N}"[..10], name = "Nueva", currencyCode = "USD", isDefault = true }
        );
        var body = await created.Content.ReadAsStringAsync();
        created.StatusCode.Should().Be(HttpStatusCode.Created, body);
        var newId = JsonDocument.Parse(body).RootElement.GetProperty("data").GetProperty("id").GetGuid();

        var state = await LoadAsync();
        state.Lists[newId].IsDefault.Should().BeTrue();
        state.Lists[_f.GeneralListId].IsDefault.Should().BeFalse();
        (await PriceListFlagLogsAsync(newId)).Should().Be(1);

        // Regla conservada: con otra predeterminada activa, crear otra predeterminada se rechaza.
        var duplicate = await client.PostAsJsonAsync(
            "/api/v1/pricing/price-lists",
            new { code = $"D{Guid.NewGuid():N}"[..10], name = "Duplicada", currencyCode = "USD", isDefault = true }
        );
        duplicate.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Update_marca_y_desmarca_la_predeterminada_auditando_y_respetando_la_regla()
    {
        await ResetAsync();
        using var client = _f.CreateClient();
        object Payload(Guid id, string name, bool isDefault) =>
            new { id, name, currencyCode = "USD", isDefault };
        var logsBefore = await PriceListFlagLogsAsync(_f.GeneralListId);

        (await client.PutAsJsonAsync($"/api/v1/pricing/price-lists/{_f.MayoristaListId}", Payload(_f.MayoristaListId, "Mayorista", true)))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "ya existe otra predeterminada activa");

        var cleared = await client.PutAsJsonAsync(
            $"/api/v1/pricing/price-lists/{_f.GeneralListId}",
            Payload(_f.GeneralListId, "General", false)
        );
        cleared.StatusCode.Should().Be(HttpStatusCode.OK, await cleared.Content.ReadAsStringAsync());
        (await LoadAsync()).Lists[_f.GeneralListId].IsDefault.Should().BeFalse();
        (await PriceListFlagLogsAsync(_f.GeneralListId)).Should().Be(logsBefore + 1);

        await DisableGeneralKeepingFlagAsync(); // GENERAL deshabilitada (ya sin bandera)
        (await client.PutAsJsonAsync($"/api/v1/pricing/price-lists/{_f.GeneralListId}", Payload(_f.GeneralListId, "General", true)))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "no se marca una lista deshabilitada");

        var marked = await client.PutAsJsonAsync(
            $"/api/v1/pricing/price-lists/{_f.MayoristaListId}",
            Payload(_f.MayoristaListId, "Mayorista", true)
        );
        marked.StatusCode.Should().Be(HttpStatusCode.OK, await marked.Content.ReadAsStringAsync());
        (await LoadAsync()).Lists[_f.MayoristaListId].IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Fallo_de_BD_durante_el_guardado_unificado_revierte_OrgSettings_PriceList_y_auditoria()
    {
        await ResetAsync();
        var before = await LoadAsync();
        var companyA = _f.CompanyAId.ToString();

        // Falla el INSERT/UPDATE de org_settings de la empresa A. Ocurre DESPUÉS de que el cambio de
        // la lista ya se envió a la BD (flush intermedio), así que prueba el rollback completo.
        using (var scope = _f.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "CREATE OR REPLACE FUNCTION zh_test_forced_org_settings_failure() RETURNS trigger AS $fn$ "
                    + "BEGIN RAISE EXCEPTION 'SALES-PREFERENCES-INTEGRITY-01 rollback forzado'; END $fn$ LANGUAGE plpgsql;"
            );
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER zh_test_forced_org_settings_failure BEFORE INSERT OR UPDATE ON org_settings "
                    + $"FOR EACH ROW WHEN (NEW.company_id = '{companyA}'::uuid) "
                    + "EXECUTE FUNCTION zh_test_forced_org_settings_failure();"
            );
        }

        try
        {
            using var client = _f.CreateClient();
            var response = await client.PutAsJsonAsync(
                Defaults,
                new
                {
                    defaultDocTypeCode = "01",
                    defaultSriPaymentMethodCode = "20",
                    defaultPaymentTermId = _f.ActiveTermId,
                    defaultPriceList = new { priceListId = _f.MayoristaListId },
                }
            );
            response.IsSuccessStatusCode.Should().BeFalse();
        }
        finally
        {
            using var scope = _f.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "DROP TRIGGER IF EXISTS zh_test_forced_org_settings_failure ON org_settings;"
            );
            await db.Database.ExecuteSqlRawAsync(
                "DROP FUNCTION IF EXISTS zh_test_forced_org_settings_failure();"
            );
        }

        var after = await LoadAsync();
        after.Settings.Should().Equal(before.Settings);
        after.Lists.Should().Equal(before.Lists);
        after.Lists[_f.GeneralListId].IsDefault.Should().BeTrue();
        after.ChangeLogCount.Should().Be(before.ChangeLogCount);
    }
}
