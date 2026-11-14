using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ERP.API.Tests.Support;
using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Expenses;

/// <summary>
/// EXPENSE-BACKEND-01 — pipeline HTTP real (<c>RuntimePermissionAuthorizer</c> contra PostgreSQL,
/// Testcontainers). Un perfil de Gastos SIN <c>accounting.view</c> carga las categorías con el código
/// y nombre de la cuenta vinculada (lo que necesita el formulario) y sigue sin poder usar
/// <c>GET /accounting/accounts</c>: la autorización contable no se relaja ni se amplía.
/// </summary>
public sealed class ExpenseCategoryAccountVisibilityFixture : IAsyncLifetime
{
    public const string Role = "Gastos";
    public const string LinkedCode = "6.1.01.001";
    public const string LinkedName = "Gasto administrativo";
    public const string UnlinkedCode = "1.1.01.001";
    public const string UnlinkedName = "Caja general reservada";

    private readonly PostgreSqlTestWebAppFactory _baseFactory = new(useHttpCompanyContext: true);
    private Guid _adminId;

    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid LinkedAccountId { get; private set; }
    public Guid SupplierId { get; private set; }
    public Guid SubcategoryId { get; private set; }
    public Guid ExpenseUpdateUserId { get; private set; }
    public Guid SupplierViewUserId { get; private set; }
    public Guid SupplierConfigureUserId { get; private set; }

    /// <summary>Gastos: catálogo y documentos, sin ningún permiso contable.</summary>
    public Guid ExpenseUserId { get; private set; }

    /// <summary>Control: solo <c>accounting.view</c>, sin permisos de Gastos.</summary>
    public Guid AccountantUserId { get; private set; }

    /// <summary>Control: ni Gastos ni Contabilidad.</summary>
    public Guid NoPermissionsUserId { get; private set; }

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

    private async Task SeedAsync()
    {
        using var scope = _baseFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        _adminId = Guid.NewGuid();
        var tenant = Tenant.Create("ZH-ExpenseAccounts", $"zh-ea-{Guid.NewGuid():N}"[..16], _adminId);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        TenantId = tenant.Id;
        _baseFactory.MutableTenant.TenantId = TenantId;

        var company = Company.CreateManaged(TenantId, "1790000000001", "Gastos S.A.", createdBy: _adminId);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        CompanyId = company.Id;

        var branch = Branch.Create(
            TenantId, "Matriz", "Av. Principal 123", "SUC-EA",
            null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, isMainBranch: true, _adminId, companyId: CompanyId
        );
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        BranchId = branch.Id;

        var linked = Account.Create(
            TenantId, CompanyId, AccountCode.Create(LinkedCode), LinkedName, null,
            AccountType.Expense, AccountNature.Debit, true, _adminId);
        var unlinked = Account.Create(
            TenantId, CompanyId, AccountCode.Create(UnlinkedCode), UnlinkedName, null,
            AccountType.Asset, AccountNature.Debit, true, _adminId);
        db.Accounts.AddRange(linked, unlinked);
        await db.SaveChangesAsync();
        LinkedAccountId = linked.Id;

        var type = ExpenseCategoryNode.CreateType(TenantId, CompanyId, "ADM", "Administrativos", _adminId);
        db.ExpenseCategoryNodes.Add(type);
        await db.SaveChangesAsync();
        var category = ExpenseCategoryNode.CreateCategory(TenantId, CompanyId, type, "OFF", "Oficina", _adminId);
        db.ExpenseCategoryNodes.Add(category);
        await db.SaveChangesAsync();
        var subcategory = ExpenseCategoryNode.CreateSubcategory(
            TenantId, CompanyId, category, "PAP", "Papeleria", linked.Id, _adminId);
        db.ExpenseCategoryNodes.Add(subcategory);
        await db.SaveChangesAsync();
        SubcategoryId = subcategory.Id;
        var supplier = BusinessPartner.Create(TenantId, "04", "1790016919001", null, "Proveedor", _adminId);
        db.BusinessPartners.Add(supplier);
        await db.SaveChangesAsync();
        SupplierId = supplier.Id;
        // El alta auditada requiere el mismo contexto de empresa que una petición real.
        using (JobExecutionContext.Begin(TenantId, CompanyId))
        {
            db.SupplierExpenseSubcategories.Add(SupplierExpenseSubcategory.Create(
                TenantId, CompanyId, SupplierId, SubcategoryId, _adminId));
            await db.SaveChangesAsync();
        }

        ExpenseUserId = await CreateUserAsync(
            db, "Gastos sin contabilidad",
            ExpensePermissions.CatalogView, ExpensePermissions.DocumentsView,
            ExpensePermissions.DocumentsCreate);
        AccountantUserId = await CreateUserAsync(db, "Solo contabilidad", AccountingPermissions.View);
        NoPermissionsUserId = await CreateUserAsync(db, "Sin permisos");
        ExpenseUpdateUserId = await CreateUserAsync(db, "Editar gastos", ExpensePermissions.DocumentsUpdate);
        SupplierViewUserId = await CreateUserAsync(db, "Consultar proveedores", MasterDataPermissions.BusinessPartnersView);
        SupplierConfigureUserId = await CreateUserAsync(db, "Configurar proveedores",
            MasterDataPermissions.BusinessPartnersView, MasterDataPermissions.BusinessPartnersConfigureCompany);
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
public sealed class ExpenseCategoryAccountVisibilityHttpTests
    : IClassFixture<ExpenseCategoryAccountVisibilityFixture>
{
    private const string Categories = "/api/v1/expenses/categories";
    private const string Accounts = "/api/v1/accounting/accounts";
    private readonly ExpenseCategoryAccountVisibilityFixture _f;

    public ExpenseCategoryAccountVisibilityHttpTests(ExpenseCategoryAccountVisibilityFixture fixture) =>
        _f = fixture;

    [Fact]
    public async Task Expense_profile_without_accounting_view_loads_categories_with_the_linked_account()
    {
        using var client = _f.CreateClientFor(_f.ExpenseUserId);

        using var response = await client.GetAsync(Categories);

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        var subcategory = FindNodes(json.RootElement.GetProperty("data"))
            .Single(n => n.GetProperty("level").GetString() == "Subcategory");
        subcategory.GetProperty("accountingAccountId").GetGuid().Should().Be(_f.LinkedAccountId);
        subcategory.GetProperty("accountingAccountCode").GetString()
            .Should().Be(ExpenseCategoryAccountVisibilityFixture.LinkedCode);
        subcategory.GetProperty("accountingAccountName").GetString()
            .Should().Be(ExpenseCategoryAccountVisibilityFixture.LinkedName);
    }

    [Fact]
    public async Task Categories_response_never_exposes_accounts_that_are_not_linked_to_a_subcategory()
    {
        using var client = _f.CreateClientFor(_f.ExpenseUserId);

        using var response = await client.GetAsync(Categories);

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        body.Should()
            .NotContain(ExpenseCategoryAccountVisibilityFixture.UnlinkedCode)
            .And.NotContain(ExpenseCategoryAccountVisibilityFixture.UnlinkedName);
    }

    [Fact]
    public async Task Expense_profile_without_accounting_view_still_cannot_use_the_chart_of_accounts()
    {
        using var client = _f.CreateClientFor(_f.ExpenseUserId);

        using var list = await client.GetAsync(Accounts);
        using var one = await client.GetAsync($"{Accounts}/{_f.LinkedAccountId}");

        list.StatusCode.Should().Be(HttpStatusCode.Forbidden, await list.Content.ReadAsStringAsync());
        one.StatusCode.Should().Be(HttpStatusCode.Forbidden, await one.Content.ReadAsStringAsync());
        (await list.Content.ReadAsStringAsync())
            .Should().NotContain(ExpenseCategoryAccountVisibilityFixture.UnlinkedCode);
    }

    [Fact]
    public async Task Accounting_view_still_authorizes_the_chart_of_accounts_and_does_not_open_expenses_catalog()
    {
        using var client = _f.CreateClientFor(_f.AccountantUserId);

        using var accounts = await client.GetAsync(Accounts);
        using var categories = await client.GetAsync(Categories);

        accounts.StatusCode.Should().Be(HttpStatusCode.OK, await accounts.Content.ReadAsStringAsync());
        categories.StatusCode.Should().Be(
            HttpStatusCode.Forbidden, await categories.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Profile_without_permissions_gets_neither_categories_nor_accounts()
    {
        using var client = _f.CreateClientFor(_f.NoPermissionsUserId);

        using var categories = await client.GetAsync(Categories);
        using var accounts = await client.GetAsync(Accounts);

        categories.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        accounts.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public async Task Supplier_scope_uses_existing_read_permissions_and_reserves_catalog_for_configuration(int profile, bool hasCatalog)
    {
        var userId = profile switch
        {
            0 => _f.ExpenseUserId,
            1 => _f.ExpenseUpdateUserId,
            2 => _f.SupplierViewUserId,
            _ => _f.SupplierConfigureUserId,
        };
        using var client = _f.CreateClientFor(userId);
        using var response = await client.GetAsync($"/api/v1/master/business-partners/{_f.SupplierId}/expense-subcategories");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        var data = json.RootElement.GetProperty("data");
        data.GetProperty("mode").GetString().Should().Be("Restricted");
        data.GetProperty("availableSubcategoryIds").EnumerateArray().Select(x => x.GetGuid())
            .Should().Equal(_f.SubcategoryId);
        var catalogPresent = data.TryGetProperty("catalog", out var catalog) && catalog.ValueKind != JsonValueKind.Null;
        catalogPresent.Should().Be(hasCatalog);
        if (hasCatalog)
            catalog.GetArrayLength().Should().Be(1);
        else
        {
            using var write = await client.PutAsJsonAsync(
                $"/api/v1/master/business-partners/{_f.SupplierId}/expense-subcategories",
                new { expenseSubcategoryIds = Array.Empty<Guid>() });
            write.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Supplier_scope_rejects_profiles_without_read_permissions(bool accountant)
    {
        using var client = _f.CreateClientFor(accountant ? _f.AccountantUserId : _f.NoPermissionsUserId);
        using var response = await client.GetAsync($"/api/v1/master/business-partners/{_f.SupplierId}/expense-subcategories");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("Papeleria");
    }

    [Fact]
    public async Task Supplier_scope_still_requires_session_and_company_membership()
    {
        using var client = _f.CreateClientFor(_f.ExpenseUserId);
        client.DefaultRequestHeaders.Remove("X-Company-Id");
        client.DefaultRequestHeaders.Add("X-Company-Id", Guid.NewGuid().ToString());
        using var foreignCompany = await client.GetAsync($"/api/v1/master/business-partners/{_f.SupplierId}/expense-subcategories");
        foreignCompany.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        client.DefaultRequestHeaders.Authorization = null;
        using var anonymous = await client.GetAsync($"/api/v1/master/business-partners/{_f.SupplierId}/expense-subcategories");
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static IEnumerable<JsonElement> FindNodes(JsonElement array)
    {
        foreach (var node in array.EnumerateArray())
        {
            yield return node;
            foreach (var child in FindNodes(node.GetProperty("children")))
                yield return child;
        }
    }
}
