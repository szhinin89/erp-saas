using System.Net;
using System.Text.Json;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Integration;

/// <summary>
/// CUSTOMER-ADVANCES-V1 CA-F6 — filtro opcional <c>customerId</c> del listado oficial de CxC, con
/// HTTP real + PostgreSQL real: alcance de tenant y empresa, paginación, total y filtros previos.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class ReceivableCustomerFilterApiTests
    : IClassFixture<FinancialCommandIdempotencyFixture>
{
    private const string Url = "/api/v1/sales-receivables";
    private readonly FinancialCommandIdempotencyFixture _f;

    public ReceivableCustomerFilterApiTests(FinancialCommandIdempotencyFixture f) => _f = f;

    private sealed record Page(int Total, List<Guid> Ids, List<Guid> Customers);

    [Fact]
    public async Task Devuelve_solo_las_CxC_del_cliente_en_la_empresa_activa()
    {
        var client = await ClientAsync();
        var customer = await _f.CreateCustomerAsync();
        var other = await _f.CreateCustomerAsync();
        var own = new[]
        {
            await _f.CreateReceivableAsync(10m, customerId: customer),
            await _f.CreateReceivableAsync(20m, customerId: customer),
        };
        await _f.CreateReceivableAsync(30m, customerId: other);
        // El socio de negocio es del tenant: también tiene deuda en otra empresa.
        var elsewhere = await _f.CreateReceivableAsync(
            40m,
            foreignCompany: true,
            customerId: customer
        );

        var page = await ListAsync(client, $"?customerId={customer}");

        page.Total.Should().Be(2);
        page.Ids.Should().BeEquivalentTo(own).And.NotContain(elsewhere);
        page.Customers.Should().OnlyContain(id => id == customer);
    }

    [Fact]
    public async Task Un_cliente_sin_CxC_o_de_otro_tenant_no_devuelve_nada()
    {
        var client = await ClientAsync();
        await _f.CreateReceivableAsync(10m);
        var foreignCustomer = await AddForeignTenantReceivableAsync();

        foreach (var customer in new[] { await _f.CreateCustomerAsync(), foreignCustomer })
        {
            var page = await ListAsync(client, $"?customerId={customer}");
            page.Total.Should().Be(0);
            page.Ids.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task Pagina_y_cuenta_en_el_servidor_despues_de_filtrar()
    {
        var client = await ClientAsync();
        var customer = await _f.CreateCustomerAsync();
        var own = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            own.Add(await _f.CreateReceivableAsync(10m + i, customerId: customer));
            // Intercaladas: sin filtrar antes de paginar, las páginas vendrían incompletas.
            await _f.CreateReceivableAsync(99m);
        }

        var pages = new List<Page>();
        for (var number = 1; number <= 4; number++)
            pages.Add(
                await ListAsync(client, $"?customerId={customer}&pageNumber={number}&pageSize=2")
            );

        pages.Select(p => p.Ids.Count).Should().Equal(2, 2, 1, 0);
        pages.Should().OnlyContain(p => p.Total == 5);
        pages.SelectMany(p => p.Ids).Should().Equal(Enumerable.Reverse(own));
    }

    [Fact]
    public async Task Se_combina_con_el_filtro_de_estado()
    {
        var client = await ClientAsync();
        var customer = await _f.CreateCustomerAsync();
        var pending = await _f.CreateReceivableAsync(10m, customerId: customer);
        var paid = await _f.CreateReceivableAsync(20m, customerId: customer);
        var cancelled = await _f.CreateReceivableAsync(30m, customerId: customer);
        await DbAsync(async db =>
        {
            var rows = await db
                .SalesReceivables.IgnoreQueryFilters()
                .Where(r => r.Id == paid || r.Id == cancelled)
                .ToListAsync();
            db.Entry(rows.Single(r => r.Id == paid)).Property(r => r.PaidAmount).CurrentValue = 20m;
            db.Entry(rows.Single(r => r.Id == cancelled)).Property(r => r.Status).CurrentValue =
                "cancelled";
        });

        async Task<List<Guid>> Ids(string status) =>
            (await ListAsync(client, $"?customerId={customer}&status={status}")).Ids;

        (await Ids("pending")).Should().Equal(pending);
        (await Ids("paid")).Should().Equal(paid);
        (await Ids("cancelled")).Should().Equal(cancelled);
        (await ListAsync(client, $"?customerId={customer}")).Total.Should().Be(3);
    }

    [Fact]
    public async Task Sin_customerId_conserva_el_listado_completo_de_la_empresa()
    {
        var client = await ClientAsync();
        var first = await _f.CreateReceivableAsync(10m, customerId: await _f.CreateCustomerAsync());
        var second = await _f.CreateReceivableAsync(
            20m,
            customerId: await _f.CreateCustomerAsync()
        );
        var expected = 0;
        await DbAsync(async db =>
            expected = await db
                .SalesReceivables.IgnoreQueryFilters()
                .CountAsync(r => r.CompanyId == _f.CompanyId)
        );

        var page = await ListAsync(client, "?pageSize=100");

        page.Total.Should().Be(expected);
        page.Ids.Should().Contain([first, second]);
    }

    [Fact]
    public async Task El_filtro_no_amplia_la_autorizacion()
    {
        var customer = await _f.CreateCustomerAsync();
        await _f.CreateReceivableAsync(10m, customerId: customer);
        var withoutSales = _f.CreateClient(
            await _f.CreateUserWithPermissionsAsync(FinancePermissions.View),
            "User"
        );
        (await withoutSales.GetAsync($"{Url}?customerId={customer}"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);

        var allowed = _f.CreateClient(
            await _f.CreateUserWithPermissionsAsync(SalesPermissions.View),
            "User"
        );
        (await ListAsync(allowed, $"?customerId={customer}")).Total.Should().Be(1);
        allowed.DefaultRequestHeaders.Remove("X-Branch-Id");
        (await allowed.GetAsync($"{Url}?customerId={customer}"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
    }

    private async Task<HttpClient> ClientAsync() =>
        _f.CreateClient((await _f.CreateOperatorAsync()).UserId);

    private static async Task<Page> ListAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync(Url + query);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        var data = JsonDocument.Parse(body).RootElement.GetProperty("data");
        var items = data.GetProperty("items").EnumerateArray().ToList();
        return new Page(
            data.GetProperty("total").GetInt32(),
            items.Select(i => i.GetProperty("id").GetGuid()).ToList(),
            items.Select(i => i.GetProperty("customerId").GetGuid()).ToList()
        );
    }

    private async Task DbAsync(Func<ErpDbContext, Task> action)
    {
        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await action(db);
        await db.SaveChangesAsync();
    }

    /// <summary>Cliente y saldo inicial de otro tenant, con sus propios maestros.</summary>
    private async Task<Guid> AddForeignTenantReceivableAsync()
    {
        var seedBy = Guid.NewGuid();
        var tenant = Tenant.Create("Ajeno", $"aj-{Guid.NewGuid():N}", seedBy);
        var customer = Guid.Empty;
        await DbAsync(async db =>
        {
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            var company = Company.CreateManaged(
                tenant.Id,
                $"177{Guid.NewGuid():N}"[..13],
                "Tenant Ajeno S.A.",
                createdBy: seedBy
            );
            var partner = BusinessPartner.Create(
                tenant.Id,
                "05",
                "1710034065",
                1,
                "Cliente ajeno",
                seedBy
            );
            db.Companies.Add(company);
            db.BusinessPartners.Add(partner);
            await db.SaveChangesAsync();
            var branch = Branch.Create(
                tenant.Id,
                "Ajena",
                "Av. 4",
                "T001",
                description: null,
                reference: null,
                postalCode: null,
                phone: null,
                secondaryPhone: null,
                email: null,
                website: null,
                managerName: null,
                managerPosition: null,
                managerEmail: null,
                managerPhone: null,
                countryId: null,
                provinceId: null,
                cantonId: null,
                parishId: null,
                latitude: null,
                longitude: null,
                openingDate: null,
                internalNotes: null,
                isMainBranch: true,
                createdBy: seedBy,
                companyId: company.Id
            );
            var batch = ImportBatch.Create(
                tenant.Id,
                company.Id,
                ImportType.InitialReceivables,
                seedBy
            );
            db.Branches.Add(branch);
            db.Add(batch);
            await db.SaveChangesAsync();
            db.SalesReceivables.Add(
                SalesReceivable.CreateInitialBalance(
                    tenant.Id,
                    company.Id,
                    branch.Id,
                    partner.Id,
                    "AJ-1",
                    new DateOnly(2026, 1, 1),
                    new DateOnly(2026, 1, 31),
                    100m,
                    batch.Id,
                    seedBy
                )
            );
            customer = partner.Id;
        });
        return customer;
    }
}
