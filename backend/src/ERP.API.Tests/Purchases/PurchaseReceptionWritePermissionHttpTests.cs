using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ERP.API.Tests.Support;
using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Purchases;

/// <summary>
/// PURCHASES-RECEPTION-WRITE-PERMISSIONS-01 (H3) — pipeline HTTP real (<c>RuntimePermissionAuthorizer</c>
/// contra PostgreSQL, Testcontainers). Las escrituras de <c>/api/v1/purchases/reception</c> exigen
/// <c>purchases.create</c> (y <c>expenses.documents.create</c> para preparar un gasto): un perfil de
/// solo consulta recibe 403 sin efectos; con el permiso, la misma solicitud supera la autorización
/// (ids inexistentes → rechazo de negocio, nunca una llamada al SRI). Incluye la importación real
/// del TXT para evidenciar H1 de extremo a extremo.
/// </summary>
public sealed class PurchaseReceptionWritePermissionFixture : IAsyncLifetime
{
    public const string Role = "Compras";
    public const string CompanyRuc = "1790000000001";

    private readonly PostgreSqlTestWebAppFactory _baseFactory = new(useHttpCompanyContext: true);
    private Guid _adminId;

    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid ViewerUserId { get; private set; }
    public Guid PurchaserUserId { get; private set; }
    public Guid ExpenseUserId { get; private set; }
    public Guid UpdaterUserId { get; private set; }

    /// <summary>Línea (sin producto) de un comprobante SIN compra: contexto "preparar compra nueva".</summary>
    public Guid NewPurchaseLineId { get; private set; }

    /// <summary>Línea (sin producto) de un comprobante CON compra borrador: contexto "modificar compra".</summary>
    public Guid ExistingPurchaseLineId { get; private set; }

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("JWT__SECRETKEY", IntegrationTestConstants.JwtSecretKey);
        Environment.SetEnvironmentVariable("JWT__ISSUER", "ZHTechnologies");
        Environment.SetEnvironmentVariable("JWT__AUDIENCE", "ERPUsers");

        await _baseFactory.InitializeAsync();
        await _baseFactory.MigrateAsync();
        await SeedAsync();

        _baseFactory.MutableTenant.TenantId = TenantId;
        _baseFactory.MutableUser.Role = Role;
        JobCompanyContext.Current = Guid.Empty;
        JobBranchContext.Current = Guid.Empty;
    }

    public async Task DisposeAsync() => await _baseFactory.DisposeAsync();

    public HttpClient CreateClientFor(Guid userId)
    {
        _baseFactory.MutableUser.UserId = userId;
        var client = _baseFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtFactory.CreateSessionJwt(TenantId, userId, role: Role)
        );
        client.DefaultRequestHeaders.Add("X-Company-Id", CompanyId.ToString());
        client.DefaultRequestHeaders.Add("X-Branch-Id", BranchId.ToString());
        return client;
    }

    public async Task<int> ReceptionDocumentCountAsync()
    {
        using var scope = _baseFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        return await db.PurchaseReceptionDocuments.IgnoreQueryFilters()
            .CountAsync(d => d.TenantId == TenantId);
    }

    private async Task SeedAsync()
    {
        using var scope = _baseFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        _adminId = Guid.NewGuid();
        var tenant = Tenant.Create("ZH-ReceptionPerms", $"zh-rp-{Guid.NewGuid():N}"[..16], _adminId);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        TenantId = tenant.Id;

        var company = Company.CreateManaged(TenantId, CompanyRuc, "Receptora S.A.", createdBy: _adminId);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        CompanyId = company.Id;

        var branch = Branch.Create(
            TenantId, "Matriz", "Av. Principal 123", "SUC-RP",
            null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, isMainBranch: true, _adminId, companyId: CompanyId
        );
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        BranchId = branch.Id;

        ViewerUserId = await CreateUserAsync(db, "Compras solo consulta", PurchasePermissions.View);
        PurchaserUserId = await CreateUserAsync(
            db, "Compras operación", PurchasePermissions.View, PurchasePermissions.Create);
        ExpenseUserId = await CreateUserAsync(
            db, "Gastos desde recepción", PurchasePermissions.View, ExpensePermissions.DocumentsCreate);
        UpdaterUserId = await CreateUserAsync(
            db, "Compras edición", PurchasePermissions.View, PurchasePermissions.Update);

        await SeedMatchingContextsAsync(db);
    }

    private async Task SeedMatchingContextsAsync(ErpDbContext db)
    {
        const string supplierRuc = "1791111111001";
        var supplier = BusinessPartner.Create(
            TenantId, TaxIdentification.SriRuc, supplierRuc, 2, "PROVEEDOR S.A.", _adminId);
        var paymentTerm = PaymentTerm.Create(TenantId, "CONT", "Contado", 1, 0, _adminId);
        db.BusinessPartners.Add(supplier);
        db.Add(paymentTerm);
        await db.SaveChangesAsync();

        NewPurchaseLineId = await AddVerifiedReceptionAsync(db, supplier.Id, new string('6', 49));
        var draftKey = new string('7', 49);
        ExistingPurchaseLineId = await AddVerifiedReceptionAsync(db, supplier.Id, draftKey);
        db.PurchaseInvoices.Add(PurchaseInvoice.CreateDraft(
            TenantId, CompanyId, BranchId, supplier.Id, "PROVEEDOR S.A.", supplierRuc, "01",
            "001-001-000000077", new DateOnly(2026, 7, 1), _adminId, paymentTerm.Id, "Contado", 1, 0,
            accessKey: draftKey));
        await db.SaveChangesAsync();
    }

    private async Task<Guid> AddVerifiedReceptionAsync(ErpDbContext db, Guid supplierId, string accessKey)
    {
        var document = PurchaseReceptionDocument.Create(
            TenantId, CompanyId, BranchId, PurchaseReceptionSourceDocType.Invoice, "1791111111001",
            "PROVEEDOR S.A.", supplierId, accessKey, $"001-001-{accessKey[..9]}", new DateOnly(2026, 7, 1),
            new DateTime(2026, 7, 1, 15, 0, 0, DateTimeKind.Utc), 10m, 1.5m, 11.5m, _adminId);
        db.PurchaseReceptionDocuments.Add(document);
        await db.SaveChangesAsync();
        var line = PurchaseReceptionLine.Create(
            document.Id, TenantId, "Producto sin vincular", quantity: 1m, unitPrice: 10m, vatCode: "2",
            taxCode: "2", vatPercentage: 15m, taxValue: 1.5m, discountPct: 0m, discount: 0m,
            lineSubtotal: 10m, totalLine: 11.5m, supplierCode: "A1");
        document.AttachSriAuthorization(
            accessKey, new DateTime(2026, 7, 1, 15, 0, 0, DateTimeKind.Utc), "<factura/>",
            new DateTime(2026, 7, 1, 16, 0, 0, DateTimeKind.Utc), [line], _adminId, docTypeCode: "01",
            sriPaymentMethodCode: "20",
            processing: new PurchaseReceptionProcessingOutcome(
                PurchaseReceptionProcessingStatus.Processed, LinesDetected: 1, LinesProcessed: 1, Notes: null));
        await db.SaveChangesAsync();
        return line.Id;
    }

    private async Task<Guid> CreateUserAsync(ErpDbContext db, string profileName, params string[] permissions)
    {
        var profile = AccessProfile.Create(TenantId, CompanyId, profileName, null, _adminId);
        db.AccessProfiles.Add(profile);
        await db.SaveChangesAsync();
        foreach (var key in permissions)
            db.AccessProfilePermissions.Add(
                AccessProfilePermission.Create(TenantId, profile.Id, key, true, _adminId));
        var user = IdentityUser.Create(
            $"usuario-{Guid.NewGuid():N}", "Usuario", "Prueba",
            $"usuario-{Guid.NewGuid():N}@example.com", "hash", _adminId);
        db.IdentityUsers.Add(user);
        await db.SaveChangesAsync();
        var membership = CompanyUserMembership.Create(CompanyId, user.Id, Role, profile.Id, _adminId);
        db.CompanyUserMemberships.Add(membership);
        await db.SaveChangesAsync();
        db.CompanyUserBranches.Add(
            CompanyUserBranch.Create(TenantId, CompanyId, membership.Id, BranchId, _adminId));
        await db.SaveChangesAsync();
        return user.Id;
    }
}

[Trait("Category", "PostgreSql")]
public sealed class PurchaseReceptionWritePermissionHttpTests
    : IClassFixture<PurchaseReceptionWritePermissionFixture>
{
    private const string Base = "/api/v1/purchases/reception";
    private readonly PurchaseReceptionWritePermissionFixture _f;

    public PurchaseReceptionWritePermissionHttpTests(PurchaseReceptionWritePermissionFixture fixture) =>
        _f = fixture;

    /// <summary>Rutas que solo preparan o registran (siempre <c>purchases.create</c>).</summary>
    public static TheoryData<string, string?> CreateOnlyRoutes()
    {
        var id = Guid.NewGuid();
        return new TheoryData<string, string?>
        {
            { $"{id}/download-xml", null },
            { "documents/download-xml-pending", $$"""{"documentIds":["{{id}}"],"onlyMissingXml":true}""" },
            { $"{id}/create-draft", null },
        };
    }

    /// <summary>Conciliación: <c>purchases.create</c> o <c>purchases.update</c> según el comprobante.</summary>
    public static TheoryData<string, string?> MatchingRoutes()
    {
        var id = Guid.NewGuid();
        return new TheoryData<string, string?>
        {
            { $"lines/{id}/match-item", $$"""{"itemId":"{{Guid.NewGuid()}}"}""" },
            { $"lines/{id}/unmatch-item", null },
            { $"lines/{id}/correct-match", $$"""{"itemId":"{{Guid.NewGuid()}}"}""" },
            { "matching/bulk", "[]" },
            { "matching/resolve", $$"""{"purchaseReceptionDocumentId":"{{id}}","lines":[]}""" },
        };
    }

    private static HttpContent? Json(string? body) =>
        body is null ? null : new StringContent(body, Encoding.UTF8, "application/json");

    private static MultipartFormDataContent Txt(string receiver)
    {
        var txt =
            "RUC_EMISOR\tRAZON_SOCIAL_EMISOR\tTIPO_COMPROBANTE\tSERIE_COMPROBANTE\tCLAVE_ACCESO\t"
            + "FECHA_AUTORIZACION\tFECHA_EMISION\tIDENTIFICACION_RECEPTOR\tVALOR_SIN_IMPUESTOS\tIVA\tIMPORTE_TOTAL\n"
            + $"1791111111001\tPROVEEDOR S.A.\tFactura\t001-001-000000001\t{new string('4', 49)}\t"
            + $"01/07/2026 10:00:00\t01/07/2026\t{receiver}\t10.00\t1.50\t11.50\n";
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(txt));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        return new MultipartFormDataContent { { file, "file", "recibidos.txt" } };
    }

    [Theory]
    [MemberData(nameof(CreateOnlyRoutes))]
    [MemberData(nameof(MatchingRoutes))]
    public async Task View_only_profile_gets_403_on_every_purchase_write_route(string route, string? body)
    {
        using var viewer = _f.CreateClientFor(_f.ViewerUserId);

        using var response = await viewer.PostAsync($"{Base}/{route}", Json(body));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [MemberData(nameof(CreateOnlyRoutes))]
    [MemberData(nameof(MatchingRoutes))]
    public async Task Create_permission_passes_authorization_on_every_purchase_write_route(string route, string? body)
    {
        using var purchaser = _f.CreateClientFor(_f.PurchaserUserId);

        using var response = await purchaser.PostAsync($"{Base}/{route}", Json(body));

        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(CreateOnlyRoutes))]
    public async Task Update_permission_never_authorizes_preparing_or_registering(string route, string? body)
    {
        using var updater = _f.CreateClientFor(_f.UpdaterUserId);

        using var response = await updater.PostAsync($"{Base}/{route}", Json(body));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Update_permission_cannot_import()
    {
        using var updater = _f.CreateClientFor(_f.UpdaterUserId);

        using var response = await updater.PostAsync(
            $"{Base}/import", Txt(PurchaseReceptionWritePermissionFixture.CompanyRuc));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Mismo endpoint, permiso según el dato: sin compra del comprobante solo <c>purchases.create</c>;
    /// con su compra borrador solo <c>purchases.update</c>. Superar la autorización deja la solicitud
    /// en la regla de negocio (la línea no tiene producto que desvincular → 422, sin escrituras).
    /// </summary>
    [Theory]
    [InlineData(false, false, HttpStatusCode.UnprocessableEntity)]
    [InlineData(false, true, HttpStatusCode.Forbidden)]
    [InlineData(true, true, HttpStatusCode.UnprocessableEntity)]
    [InlineData(true, false, HttpStatusCode.Forbidden)]
    public async Task Matching_requires_create_for_a_new_purchase_and_update_for_an_existing_one(
        bool existingPurchase,
        bool asUpdater,
        HttpStatusCode expected
    )
    {
        var lineId = existingPurchase ? _f.ExistingPurchaseLineId : _f.NewPurchaseLineId;
        using var client = _f.CreateClientFor(asUpdater ? _f.UpdaterUserId : _f.PurchaserUserId);

        using var response = await client.PostAsync($"{Base}/lines/{lineId}/unmatch-item", null);

        response.StatusCode.Should().Be(expected, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task View_only_profile_cannot_import_and_nothing_is_written()
    {
        var before = await _f.ReceptionDocumentCountAsync();
        using var viewer = _f.CreateClientFor(_f.ViewerUserId);

        using var response = await viewer.PostAsync($"{Base}/import", Txt(PurchaseReceptionWritePermissionFixture.CompanyRuc));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _f.ReceptionDocumentCountAsync()).Should().Be(before);
    }

    [Fact]
    public async Task Import_rejects_a_foreign_receiver_and_accepts_the_company_ruc()
    {
        using var purchaser = _f.CreateClientFor(_f.PurchaserUserId);
        var before = await _f.ReceptionDocumentCountAsync();

        using var foreign = await purchaser.PostAsync($"{Base}/import", Txt("0990000000001"));
        foreign.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await foreign.Content.ReadAsStringAsync()).Should().NotContain("0990000000001");
        (await _f.ReceptionDocumentCountAsync()).Should().Be(before);

        using var own = await purchaser.PostAsync(
            $"{Base}/import", Txt(PurchaseReceptionWritePermissionFixture.CompanyRuc));
        own.StatusCode.Should().Be(HttpStatusCode.OK, await own.Content.ReadAsStringAsync());
        (await _f.ReceptionDocumentCountAsync()).Should().Be(before + 1);
    }

    [Fact]
    public async Task Expense_draft_requires_the_expense_create_permission()
    {
        var route = $"{Base}/{Guid.NewGuid()}/create-expense-draft";
        using var purchaser = _f.CreateClientFor(_f.PurchaserUserId);
        using var denied = await purchaser.PostAsync(route, null);
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var expenseUser = _f.CreateClientFor(_f.ExpenseUserId);
        using var allowed = await expenseUser.PostAsync(route, null);
        allowed.StatusCode.Should().Be(HttpStatusCode.NotFound, await allowed.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task View_only_profile_keeps_the_read_routes()
    {
        using var viewer = _f.CreateClientFor(_f.ViewerUserId);
        var id = Guid.NewGuid();

        using var refresh = await viewer.PostAsync(
            $"{Base}/documents/refresh-status", Json($$"""{"documentIds":["{{id}}"]}"""));
        using var xmlView = await viewer.GetAsync($"{Base}/documents/{id}/xml-view");
        using var lines = await viewer.GetAsync($"{Base}/{id}/lines");
        using var line = await viewer.GetAsync($"{Base}/lines/{id}");

        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        new[] { xmlView, lines, line }.Should().OnlyContain(r => r.StatusCode != HttpStatusCode.Forbidden);
    }
}
