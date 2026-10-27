using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ERP.API.Tests.Support;
using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Integration;

/// <summary>
/// SEC-03 — operaciones COMPANY que estaban sin marcador de scope (Communications, Retenciones,
/// OrgConfig, política fiscal de ventas, preferencias operativas, estado de facturación
/// electrónica). Prueba HTTP real contra PostgreSQL (Testcontainers) con <c>CurrentCompanyService</c>
/// real (X-Company-Id), mismo patrón que <see cref="InventoryAdjustmentReasonCompanyIsolationFixture"/>.
/// El usuario NO es admin: membership solo en Empresa A con un AccessProfile granular, de modo que
/// <c>RuntimePermissionAuthorizer</c> concede los permisos por la única membresía del tenant aunque
/// el header apunte a B — el rechazo de B debe venir de <c>CompanyScopeBehavior</c>.
/// </summary>
public sealed class CompanySettingsIsolationFixture : IAsyncLifetime
{
    public const string Role = "Cajero";
    public const string SmtpHostB = "smtp.empresa-b.example";
    public const string ConsumerFinalMaxAmountB = "50.00";

    private readonly PostgreSqlTestWebAppFactory _baseFactory = new(useHttpCompanyContext: true);
    private Guid _adminId;

    public Guid TenantId { get; private set; }
    public Guid CompanyAId { get; private set; }
    public Guid CompanyBId { get; private set; }
    public Guid BranchAId { get; private set; }
    public Guid BranchBId { get; } = Guid.NewGuid();
    public Guid WarehouseBId { get; } = Guid.NewGuid();
    public Guid UserId { get; private set; }

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("JWT__SECRETKEY", IntegrationTestConstants.JwtSecretKey);
        Environment.SetEnvironmentVariable("JWT__ISSUER", "ZHTechnologies");
        Environment.SetEnvironmentVariable("JWT__AUDIENCE", "ERPUsers");

        await _baseFactory.InitializeAsync();
        await _baseFactory.MigrateAsync();
        await SeedAsync();

        _baseFactory.MutableTenant.TenantId = TenantId;
        _baseFactory.MutableUser.UserId = UserId;
        _baseFactory.MutableUser.Role = Role;
        JobCompanyContext.Current = Guid.Empty;
        JobBranchContext.Current = Guid.Empty;
    }

    public async Task DisposeAsync() => await _baseFactory.DisposeAsync();

    public HttpClient CreateClient(Guid? companyId)
    {
        var client = _baseFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtFactory.CreateSessionJwt(TenantId, UserId, role: Role)
        );
        if (companyId is { } id)
            client.DefaultRequestHeaders.Add("X-Company-Id", id.ToString());
        client.DefaultRequestHeaders.Add("X-Branch-Id", BranchAId.ToString());
        return client;
    }

    public IServiceScope CreateScope() => _baseFactory.Services.CreateScope();

    private async Task SeedAsync()
    {
        using var scope = _baseFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        _adminId = Guid.NewGuid();
        var tenant = Tenant.Create("ZH-CompanySettingsIsolation", $"zh-csi-{Guid.NewGuid():N}", _adminId);
        db.Tenants.Add(tenant);
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
        await db.SaveChangesAsync();

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
        await db.SaveChangesAsync();
        BranchAId = branchA.Id;

        // Configuración de Empresa B (SMTP, política fiscal, sucursal) — sembrada directamente: el
        // usuario nunca tiene acceso a B.
        db.OrgSettings.AddRange(
            OrgSetting.Create(
                TenantId,
                CompanyBId,
                OrgScope.Company,
                CompanyBId,
                OrgSettingKeys.Communications.SmtpHost,
                SmtpHostB,
                SettingDataType.String,
                _adminId
            ),
            OrgSetting.Create(
                TenantId,
                CompanyBId,
                OrgScope.Company,
                CompanyBId,
                OrgSettingKeys.Sales.ConsumerFinalMaxAmount,
                ConsumerFinalMaxAmountB,
                SettingDataType.Decimal,
                _adminId
            ),
            OrgSetting.Create(
                TenantId,
                CompanyBId,
                OrgScope.Branch,
                BranchBId,
                OrgSettingKeys.Invoice.DefaultWarehouseId,
                WarehouseBId.ToString(),
                SettingDataType.Guid,
                _adminId
            )
        );
        await db.SaveChangesAsync();

        var profile = AccessProfile.Create(
            TenantId,
            CompanyAId,
            "SEC-03 Operador A",
            null,
            _adminId
        );
        db.AccessProfiles.Add(profile);
        await db.SaveChangesAsync();
        foreach (
            var key in new[]
            {
                "communications.view",
                "communications.configure",
                "erp.companies.view",
                "erp.companies.update",
                "settings.company.view",
                "settings.operations.view",
                "settings.operations.configure",
                "purchases.view",
                "purchases.update",
                "electronic-documents.retry",
                "masterdata.businesspartners.view",
            }
        )
            db.AccessProfilePermissions.Add(
                AccessProfilePermission.Create(TenantId, profile.Id, key, true, _adminId)
            );
        await db.SaveChangesAsync();

        var user = IdentityUser.Create(
            $"usuario-{Guid.NewGuid():N}",
            "Usuario",
            "Prueba",
            $"usuario-{Guid.NewGuid():N}@example.com",
            "hash",
            _adminId
        );
        db.IdentityUsers.Add(user);
        await db.SaveChangesAsync();
        UserId = user.Id;

        // Membership solo en Empresa A.
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
public sealed class CompanySettingsIsolationHttpTests
    : IClassFixture<CompanySettingsIsolationFixture>
{
    private const string EmailSettings = "/api/v1/communications/email-settings";
    private const string FiscalPolicy = "/api/v1/companies/profile/fiscal-policy";
    private const string OperationalPreferences = "/api/v1/settings/operations";
    private const string CompanyInvoiceDefaults = "/api/v1/org-config/company/invoice-defaults";
    private const string ElectronicInvoicingStatus = "/api/v1/electronic-invoicing/status?checkConnectivity=false";
    private const string Retentions = "/api/v1/retentions";
    private const string CompanyScopeForbidden = "COMPANY_SCOPE_FORBIDDEN";

    private readonly CompanySettingsIsolationFixture _f;

    public CompanySettingsIsolationHttpTests(CompanySettingsIsolationFixture fixture) => _f = fixture;

    private static object NewEmailSettings(string host) =>
        new
        {
            enabled = true,
            smtpHost = host,
            smtpPort = 587,
            smtpUsername = "usuario",
            smtpPassword = "secreto",
            senderEmail = "facturas@atacante.example",
            senderName = "Atacante",
            useSsl = true,
            replyToEmail = (string?)null,
            maxRetries = 3,
            defaultLanguage = "es",
        };

    private static object NewCashPreferences() =>
        new
        {
            cash = new
            {
                requireOpeningAmount = true,
                allowCloseWithDifference = true,
                maxAllowedDifference = 5m,
                requireReasonForDifference = true,
                allowManualInOutMovements = true,
                requireReasonForMovements = true,
            },
        };

    private static async Task AssertCompanyScopeForbiddenAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, body);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("code").GetString().Should().Be(CompanyScopeForbidden);
    }

    private async Task<List<(Guid ScopeId, string Key, string? Value)>> LoadCompanyBSettingsAsync()
    {
        using var scope = _f.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var rows = await db.OrgSettings.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => s.TenantId == _f.TenantId && s.CompanyId == _f.CompanyBId)
            .Select(s => new { s.ScopeId, s.Key, s.Value })
            .ToListAsync();
        return rows.OrderBy(r => r.Key).ThenBy(r => r.ScopeId)
            .Select(r => (r.ScopeId, r.Key, r.Value))
            .ToList();
    }

    [Fact]
    public async Task Communications_desde_A_no_lee_modifica_ni_usa_el_SMTP_de_B()
    {
        var before = await LoadCompanyBSettingsAsync();
        using var asB = _f.CreateClient(_f.CompanyBId);

        await AssertCompanyScopeForbiddenAsync(await asB.GetAsync(EmailSettings));
        await AssertCompanyScopeForbiddenAsync(
            await asB.PutAsJsonAsync(EmailSettings, NewEmailSettings("smtp.atacante.example"))
        );
        await AssertCompanyScopeForbiddenAsync(
            await asB.PostAsJsonAsync($"{EmailSettings}/test", new { toEmail = "destino@example.com" })
        );

        (await LoadCompanyBSettingsAsync()).Should().Equal(before);
        before.Should().Contain(s =>
            s.Key == OrgSettingKeys.Communications.SmtpHost
            && s.Value == CompanySettingsIsolationFixture.SmtpHostB
        );

        using var asA = _f.CreateClient(_f.CompanyAId);
        var own = await asA.GetAsync(EmailSettings);
        var ownBody = await own.Content.ReadAsStringAsync();
        own.StatusCode.Should().Be(HttpStatusCode.OK, ownBody);
        ownBody.Should().NotContain(CompanySettingsIsolationFixture.SmtpHostB);
    }

    [Fact]
    public async Task Retenciones_desde_A_no_generan_ni_transmiten_comprobantes_con_contexto_de_B()
    {
        using var scope = _f.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var documentsBefore = await db.ElectronicDocuments.IgnoreQueryFilters()
            .CountAsync(d => d.TenantId == _f.TenantId);
        var retentionId = Guid.NewGuid();

        using var asB = _f.CreateClient(_f.CompanyBId);
        await AssertCompanyScopeForbiddenAsync(
            await asB.GetAsync($"{Retentions}/{retentionId}/electronic/xml")
        );
        await AssertCompanyScopeForbiddenAsync(
            await asB.GetAsync($"{Retentions}/{retentionId}/ride/pdf")
        );
        await AssertCompanyScopeForbiddenAsync(
            await asB.PostAsync($"{Retentions}/{retentionId}/electronic/register", null)
        );

        (await db.ElectronicDocuments.IgnoreQueryFilters().CountAsync(d => d.TenantId == _f.TenantId))
            .Should().Be(documentsBefore);

        // En A el request sí llega al handler (y la retención inexistente es 404, no 403).
        using var asA = _f.CreateClient(_f.CompanyAId);
        (await asA.GetAsync($"{Retentions}/{retentionId}/electronic/xml"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await asA.GetAsync($"{Retentions}/{retentionId}/ride/pdf"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OrgConfig_de_empresa_y_sucursal_no_cruza_a_B()
    {
        var before = await LoadCompanyBSettingsAsync();
        using var asB = _f.CreateClient(_f.CompanyBId);

        await AssertCompanyScopeForbiddenAsync(await asB.GetAsync(CompanyInvoiceDefaults));
        await AssertCompanyScopeForbiddenAsync(
            await asB.PutAsJsonAsync(
                CompanyInvoiceDefaults,
                new
                {
                    defaultDocTypeCode = (string?)null,
                    defaultSriPaymentMethodCode = (string?)null,
                    defaultPaymentTermId = (Guid?)null,
                }
            )
        );
        await AssertCompanyScopeForbiddenAsync(
            await asB.GetAsync($"/api/v1/org-config/branch/{_f.BranchBId}/invoice-defaults")
        );

        using var asA = _f.CreateClient(_f.CompanyAId);
        var ownCompany = await asA.GetAsync(CompanyInvoiceDefaults);
        ownCompany.StatusCode.Should().Be(HttpStatusCode.OK, await ownCompany.Content.ReadAsStringAsync());

        // Alcance de sucursal (validación existente): desde A, la sucursal de B no expone sus
        // valores ni admite escritura.
        var foreignBranch = await asA.GetAsync($"/api/v1/org-config/branch/{_f.BranchBId}/invoice-defaults");
        var foreignBranchBody = await foreignBranch.Content.ReadAsStringAsync();
        foreignBranch.StatusCode.Should().Be(HttpStatusCode.OK, foreignBranchBody);
        foreignBranchBody.Should().NotContain(_f.WarehouseBId.ToString());

        var foreignBranchWrite = await asA.PutAsJsonAsync(
            $"/api/v1/org-config/branch/{_f.BranchBId}/invoice-defaults",
            new { branchId = _f.BranchBId, defaultWarehouseId = (Guid?)null }
        );
        foreignBranchWrite.IsSuccessStatusCode.Should().BeFalse();

        var ownBranch = await asA.GetAsync($"/api/v1/org-config/branch/{_f.BranchAId}/invoice-defaults");
        ownBranch.StatusCode.Should().Be(HttpStatusCode.OK, await ownBranch.Content.ReadAsStringAsync());

        (await LoadCompanyBSettingsAsync()).Should().Equal(before);
    }

    [Fact]
    public async Task Politica_fiscal_de_ventas_de_B_no_se_lee_ni_modifica_desde_A()
    {
        var before = await LoadCompanyBSettingsAsync();
        using var asB = _f.CreateClient(_f.CompanyBId);

        await AssertCompanyScopeForbiddenAsync(await asB.GetAsync(FiscalPolicy));
        await AssertCompanyScopeForbiddenAsync(
            await asB.PutAsJsonAsync(FiscalPolicy, new { consumerFinalMaxAmount = 0m })
        );
        (await LoadCompanyBSettingsAsync()).Should().Equal(before);

        using var asA = _f.CreateClient(_f.CompanyAId);
        var update = await asA.PutAsJsonAsync(FiscalPolicy, new { consumerFinalMaxAmount = 75m });
        update.StatusCode.Should().Be(HttpStatusCode.OK, await update.Content.ReadAsStringAsync());
        (await asA.GetAsync(FiscalPolicy)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _f.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var ownValue = await db.OrgSettings.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s =>
                s.TenantId == _f.TenantId
                && s.CompanyId == _f.CompanyAId
                && s.Key == OrgSettingKeys.Sales.ConsumerFinalMaxAmount
            )
            .Select(s => s.Value)
            .SingleAsync();
        ownValue.Should().Be("75.00");
        (await LoadCompanyBSettingsAsync()).Should().Contain(s =>
            s.Key == OrgSettingKeys.Sales.ConsumerFinalMaxAmount
            && s.Value == CompanySettingsIsolationFixture.ConsumerFinalMaxAmountB
        );
    }

    [Fact]
    public async Task Preferencias_operativas_de_B_no_se_leen_ni_modifican_desde_A()
    {
        var before = await LoadCompanyBSettingsAsync();
        using var asB = _f.CreateClient(_f.CompanyBId);

        await AssertCompanyScopeForbiddenAsync(await asB.GetAsync(OperationalPreferences));
        await AssertCompanyScopeForbiddenAsync(
            await asB.PutAsJsonAsync(OperationalPreferences, NewCashPreferences())
        );
        (await LoadCompanyBSettingsAsync()).Should().Equal(before);

        using var asA = _f.CreateClient(_f.CompanyAId);
        (await asA.GetAsync(OperationalPreferences)).StatusCode.Should().Be(HttpStatusCode.OK);
        var update = await asA.PutAsJsonAsync(OperationalPreferences, NewCashPreferences());
        update.StatusCode.Should().Be(HttpStatusCode.OK, await update.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Estado_de_facturacion_electronica_de_B_no_se_expone_a_A()
    {
        using var asB = _f.CreateClient(_f.CompanyBId);
        await AssertCompanyScopeForbiddenAsync(await asB.GetAsync(ElectronicInvoicingStatus));

        using var asA = _f.CreateClient(_f.CompanyAId);
        var own = await asA.GetAsync(ElectronicInvoicingStatus);
        own.StatusCode.Should().Be(HttpStatusCode.OK, await own.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Sin_empresa_seleccionada_se_rechaza_y_las_operaciones_TENANT_siguen_funcionando()
    {
        using var noCompany = _f.CreateClient(null);
        await AssertCompanyScopeForbiddenAsync(await noCompany.GetAsync(EmailSettings));
        await AssertCompanyScopeForbiddenAsync(await noCompany.GetAsync(FiscalPolicy));
        await AssertCompanyScopeForbiddenAsync(await noCompany.GetAsync(OperationalPreferences));
        await AssertCompanyScopeForbiddenAsync(await noCompany.GetAsync(CompanyInvoiceDefaults));
        await AssertCompanyScopeForbiddenAsync(await noCompany.GetAsync(ElectronicInvoicingStatus));

        // BusinessPartner es TENANT (ITenantScopedRequest): no exige empresa operativa.
        var tenantScoped = await noCompany.GetAsync("/api/v1/master/business-partners");
        tenantScoped.StatusCode.Should().Be(HttpStatusCode.OK, await tenantScoped.Content.ReadAsStringAsync());
    }
}
