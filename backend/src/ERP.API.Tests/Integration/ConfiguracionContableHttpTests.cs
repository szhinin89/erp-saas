using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Contracts;
using ERP.API.Tests.Support;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Integration;

/// <summary>Tests HTTP end-to-end: configuraciÃ³n contable por tenant.</summary>
public sealed class ConfiguracionContableHttpTests
{
    private static async Task<(IntegrationTestWebAppFactory Factory, HttpClient Client, IntegrationSeedData.SeedResult Seed)>
        CreateClientAsync()
    {
        var factory = new IntegrationTestWebAppFactory();

        using var scope = factory.Services.CreateScope();
        var db   = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var seed = await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None, factory.MutableCompany);

        var token  = TestJwtFactory.CreateSessionJwt(seed.SubscriberId, seed.UserId, seed.CompanyId);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return (factory, client, seed);
    }

    [Fact]
    public async Task ConfiguracionContable_Get_sin_token_responde_401()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var client = factory.CreateClient();

        var res = await client.GetAsync("/api/finance/config");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ConfiguracionContable_Get_con_token_responde_200()
    {
        var (factory, client, _) = await CreateClientAsync();
        await using var _ = factory;

        var res = await client.GetAsync("/api/finance/config");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"success\":true");
    }

    [Fact]
    public async Task ConfiguracionContable_Put_con_cuenta_invalida_responde_400()
    {
        var (factory, client, _) = await CreateClientAsync();
        await using var _ = factory;

        var payload = new
        {
            inventoryAccountId = Guid.NewGuid(),
            suppliersAccountId = Guid.NewGuid(),
        };

        var res = await client.PutAsJsonAsync("/api/finance/config", payload);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"success\":false");
    }

    [Fact]
    public async Task ConfiguracionContable_Put_valido_y_crud_gastos_funciona()
    {
        var (factory, client, seed) = await CreateClientAsync();
        await using var _ = factory;

        // crear cuentas adicionales para el tenant
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

            var inv = Account.Create(seed.SubscriberId, "1.1.10", "Inventario INT", AccountType.Asset, AccountNature.Debit, seed.UserId);
            var prov = Account.Create(seed.SubscriberId, "2.1.10", "Suppliers INT", AccountType.Liability, AccountNature.Credit, seed.UserId);
            var ventas = Account.Create(seed.SubscriberId, "4.1.10", "Ventas INT", AccountType.Revenue, AccountNature.Credit, seed.UserId);
            var clientes = Account.Create(seed.SubscriberId, "1.1.20", "Clientes INT", AccountType.Asset, AccountNature.Debit, seed.UserId);
            var ivaV = Account.Create(seed.SubscriberId, "2.1.20", "IVA ventas INT", AccountType.Liability, AccountNature.Credit, seed.UserId);
            var gasto = Account.Create(seed.SubscriberId, "5.1.10", "Luz", AccountType.Expense, AccountNature.Debit, seed.UserId);

            db.Accounts.AddRange(inv, prov, ventas, clientes, ivaV, gasto);
            await db.SaveChangesAsync();
        }

        // leer cuentas para usar ids reales
        Guid invId, provId, ventasId, clientesId, ivaVentasId, gastoId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            invId       = db.Accounts.First(a => a.SubscriberId == seed.SubscriberId && a.Code.Value == "1.1.10").Id;
            provId      = db.Accounts.First(a => a.SubscriberId == seed.SubscriberId && a.Code.Value == "2.1.10").Id;
            ventasId    = db.Accounts.First(a => a.SubscriberId == seed.SubscriberId && a.Code.Value == "4.1.10").Id;
            clientesId  = db.Accounts.First(a => a.SubscriberId == seed.SubscriberId && a.Code.Value == "1.1.20").Id;
            ivaVentasId = db.Accounts.First(a => a.SubscriberId == seed.SubscriberId && a.Code.Value == "2.1.20").Id;
            gastoId     = db.Accounts.First(a => a.SubscriberId == seed.SubscriberId && a.Code.Value == "5.1.10").Id;
        }

        // upsert config OK
        var putPayload = new
        {
            inventoryAccountId = invId,
            suppliersAccountId = provId,
            salesAccountId = ventasId,
            customersAccountId = clientesId,
            vatSalesAccountId = ivaVentasId,
        };

        var putRes = await client.PutAsJsonAsync("/api/finance/config", putPayload);
        putRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // create gasto mapping
        var postRes = await client.PostAsJsonAsync("/api/finance/config/gastos", new
        {
            category = "Luz",
            expenseAccountId = gastoId,
        });
        postRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // list gasto mappings
        var listRes = await client.GetAsync("/api/finance/config/gastos");
        listRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await listRes.Content.ReadAsStringAsync();
        listBody.Should().Contain("\"success\":true");
        listBody.Should().Contain("\"category\":\"Luz\"");

        // delete
        var created = await postRes.Content.ReadFromJsonAsync<ApiResponse<ExpenseCategoryDto>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        created.Should().NotBeNull();
        created!.Success.Should().BeTrue();
        created.ResponseObject.Should().NotBeNull();
        var id = created.ResponseObject!.Id;

        var delRes = await client.DeleteAsync($"/api/finance/config/gastos/{id}");
        delRes.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}



