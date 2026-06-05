using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Contracts;
using ERP.API.Tests.Support;
using ERP.Application.Modules.Cash.DTOs;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Integration;

/// <summary>Tests HTTP del mÃ³dulo Caja / Bancos (extractos, caja chica, flujo real).</summary>
public sealed class CashHttpIntegrationTests
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
            new AuthenticationHeaderValue("Bearer", token);

        return (factory, client, seed);
    }

    [Fact]
    public async Task Caja_cuentas_bancarias_sin_token_401()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var client = factory.CreateClient();

        var res = await client.GetAsync("/api/cash/bank/cuentas-bancarias");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Caja_importar_extracto_csv_y_listar_funciona()
    {
        var (factory, client, seed) = await CreateClientAsync();
        await using var _ = factory;

        Guid cuentaGlId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            cuentaGlId = await db.Accounts
                .Where(a => a.SubscriberId == seed.SubscriberId && a.Code.Value == "1.1.99")
                .Select(a => a.Id)
                .FirstAsync();
        }

        var crear = await client.PostAsJsonAsync(
            "/api/cash/bank/cuentas-bancarias",
            new
            {
                name = "Banco INT",
                accountNumber = "1002999",
                accountType = "Corriente",
                currency = "USD",
                initialBalance = 1000m,
                ledgerAccountId = cuentaGlId,
            });
        crear.StatusCode.Should().Be(HttpStatusCode.Created);

        var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var cuentaId = (await crear.Content.ReadFromJsonAsync<ApiResponse<BankAccountDto>>(jsonOpts))!.ResponseObject.Id;

        const string csv = """
            fecha,descripcion,debito,credito
            2026-05-01,Cargo prueba,50,0
            2026-05-02,Abono prueba,0,25
            """;

        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv))
        {
            Headers = { ContentType = new MediaTypeHeaderValue("text/csv") },
        }, "file", "extracto.csv");
        form.Add(new StringContent(cuentaId.ToString()), "bankAccountId");
        form.Add(new StringContent("2026-05-01T00:00:00Z"), "periodFrom");
        form.Add(new StringContent("2026-05-02T23:59:59Z"), "periodTo");
        form.Add(new StringContent("1000"), "openingBalance");
        form.Add(new StringContent("975"), "closingBalance");

        var import = await client.PostAsync("/api/cash/bank/extractos/importar", form);
        import.StatusCode.Should().Be(HttpStatusCode.Created);

        var list = await client.GetAsync($"/api/cash/bank/extractos?cuentaBancariaId={cuentaId}");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await list.Content.ReadAsStringAsync();
        listBody.Should().Contain("\"success\":true");

        var StatementId = (await import.Content.ReadFromJsonAsync<ApiResponse<BankStatementDto>>(jsonOpts))!.ResponseObject.Id;
        var det = await client.GetAsync($"/api/cash/bank/extractos/{StatementId}");
        det.StatusCode.Should().Be(HttpStatusCode.OK);
        var detBody = await det.Content.ReadAsStringAsync();
        detBody.Should().Contain("\"success\":true");
        detBody.Should().Contain("Cargo prueba");
    }

    [Fact]
    public async Task Caja_caja_chica_gasto_y_flujo_real_responden_200()
    {
        var (factory, client, seed) = await CreateClientAsync();
        await using var _ = factory;

        var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        Guid cuentaGlId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            cuentaGlId = await db.Accounts
                .Where(a => a.SubscriberId == seed.SubscriberId && a.Code.Value == "1.1.99")
                .Select(a => a.Id)
                .FirstAsync();
        }

        var crearCaja = await client.PostAsJsonAsync(
            "/api/cash/bank/cajas-chicas",
            new { name = "Caja INT", assignedBalance = 200m, replenishmentBankAccountId = (Guid?)null, ledgerCashAccountId = cuentaGlId });
        crearCaja.StatusCode.Should().Be(HttpStatusCode.Created);
        var cajaId = (await crearCaja.Content.ReadFromJsonAsync<ApiResponse<PettyCashDto>>(jsonOpts))!.ResponseObject.Id;

        var gasto = await client.PostAsJsonAsync(
            "/api/cash/bank/caja-chica/gastos",
            new
            {
                pettyCashId = cajaId,
                date = DateTime.UtcNow,
                description = "Utiles",
                amount = 10m,
                voucherType = "Recibo",
                voucherNumber = "R-1",
            });
        gasto.StatusCode.Should().Be(HttpStatusCode.Created);

        var flujo = await client.GetAsync(
            "/api/cash/bank/flujo-efectivo/real?desde=2026-01-01&hasta=2026-12-31");
        flujo.StatusCode.Should().Be(HttpStatusCode.OK);
        (await flujo.Content.ReadAsStringAsync()).Should().Contain("\"success\":true");
    }

}

