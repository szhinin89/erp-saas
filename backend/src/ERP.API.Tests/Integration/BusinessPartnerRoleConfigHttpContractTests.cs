using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ERP.API.Tests.Support;
using ERP.Domain.Access.Entities;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Integration;

/// <summary>
/// ZH-API-THIN-BP-ROLES-01 — contrato HTTP de <c>/business-partners/{bpId}/roles</c> (pipeline real:
/// validators, handlers, ExceptionMiddleware, PostgreSQL) que debe mantenerse idéntico al mover la
/// construcción de SupplierRoleConfig/CarrierRoleConfig/CustomerRoleConfig del controller a
/// Application. El mensaje esperado de cada invariante se obtiene del propio value object de
/// Domain (es lo que el controller devolvía antes: <c>ArgumentException.Message</c>).
/// Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class BusinessPartnerRoleConfigHttpContractTests : IAsyncLifetime
{
    private readonly PostgreSqlTestWebAppFactory _factory = new();
    private HttpClient _client = null!;
    private Guid _tenantId;
    private Guid _companyId;
    private readonly Guid _adminId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("JWT__SECRETKEY", IntegrationTestConstants.JwtSecretKey);
        Environment.SetEnvironmentVariable("JWT__ISSUER", "ZHTechnologies");
        Environment.SetEnvironmentVariable("JWT__AUDIENCE", "ERPUsers");

        await _factory.InitializeAsync();
        await _factory.MigrateAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var tenant = Tenant.Create("ZH-Roles", $"zh-{Guid.NewGuid():N}", _adminId);
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            _tenantId = tenant.Id;

            var company = Company.CreateManaged(
                tenant.Id,
                $"179{tenant.Id:N}"[..13],
                "Empresa Test",
                createdBy: _adminId
            );
            db.Companies.Add(company);
            await db.SaveChangesAsync();
            _companyId = company.Id;

            var user = IdentityUser.Create(
                "sadmi",
                "Admin",
                "Test",
                $"admin-{Guid.NewGuid():N}@test.com",
                "TEST_PASSWORD_HASH",
                _adminId
            );
            db.IdentityUsers.Add(user);
            await db.SaveChangesAsync();
            db.CompanyUserMemberships.Add(
                CompanyUserMembership.Create(_companyId, user.Id, "Admin", null, _adminId)
            );
            await db.SaveChangesAsync();
        }

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtFactory.CreateSessionJwt(_tenantId, _adminId, _companyId, "Admin")
        );
        _factory.MutableTenant.TenantId = _tenantId;
        _factory.MutableCompany.CompanyId = _companyId;
        _factory.MutableUser.UserId = _adminId;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string Roles(string bpId) => $"/api/v1/master/business-partners/{bpId}/roles";

    private async Task<string> CreateBusinessPartnerAsync(string ruc, string name)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/master/business-partners",
            new
            {
                identificationType = "04",
                identificationNumber = ruc,
                legalName = name,
                countryCode = "EC",
            }
        );
        response
            .StatusCode.Should()
            .Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data")
            .GetProperty("id")
            .GetString()!;
    }

    private async Task<string> AssignAsync(string bpId, string roleType)
    {
        var response = await _client.PostAsJsonAsync(Roles(bpId), new { roleType });
        response
            .StatusCode.Should()
            .Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data")
            .GetProperty("id")
            .GetString()!;
    }

    private Task<HttpResponseMessage> Patch(
        string bpId,
        string roleId,
        string config,
        object body
    ) => _client.PatchAsJsonAsync($"{Roles(bpId)}/{roleId}/{config}", body);

    private static string DomainMessage(Action createValueObject)
    {
        var ex = FluentActions
            .Invoking(createValueObject)
            .Should()
            .Throw<ArgumentException>()
            .Which;
        return ex.Message;
    }

    /// <summary>Contrato histórico de un invariante violado: 400, code BadRequest, data.errors = [mensaje].</summary>
    private static async Task AssertInvariantResponse(
        HttpResponseMessage response,
        string expectedMessage,
        string because
    )
    {
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, $"{because}: {body}");
        var json = JsonDocument.Parse(body).RootElement;
        json.GetProperty("code").GetString().Should().Be("BAD_REQUEST", because);
        json.GetProperty("data")
            .GetProperty("errors")
            .EnumerateArray()
            .Select(e => e.GetString())
            .Should()
            .Equal(new[] { expectedMessage }, because);
    }

    private static string Long(int length) => new('X', length);

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Configs_validas_responden_200_en_los_PATCH_y_201_en_Assign_con_config()
    {
        var bp = await CreateBusinessPartnerAsync("1791352688001", "QUALA ECUADOR S A");
        var supplier = await AssignAsync(bp, "Supplier");
        var carrier = await AssignAsync(bp, "Carrier");

        (
            await Patch(
                bp,
                supplier,
                "supplier-config",
                new { isRetentionExempt = true, defaultTaxSupportCode = "  " }
            )
        )
            .StatusCode.Should()
            .Be(HttpStatusCode.OK);
        var carrierResponse = await Patch(
            bp,
            carrier,
            "carrier-config",
            new { transportAuthorizationNumber = "  AUT-1  ", vehicleCapacityTons = 12.5m }
        );
        carrierResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var carrierConfig = (await carrierResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data")
            .GetProperty("carrierConfig");
        carrierConfig
            .GetProperty("transportAuthorizationNumber")
            .GetString()
            .Should()
            .Be("AUT-1", "Domain sigue normalizando (trim)");

        var assignWithConfig = await _client.PostAsJsonAsync(
            Roles(bp),
            new { roleType = "Customer", customerConfig = new { salesZone = "  Norte  " } }
        );
        assignWithConfig
            .StatusCode.Should()
            .Be(HttpStatusCode.Created, await assignWithConfig.Content.ReadAsStringAsync());
        var customerRole = (
            await assignWithConfig.Content.ReadFromJsonAsync<JsonElement>()
        ).GetProperty("data");
        customerRole
            .GetProperty("customerConfig")
            .GetProperty("salesZone")
            .GetString()
            .Should()
            .Be("Norte");

        (
            await Patch(
                bp,
                customerRole.GetProperty("id").GetString()!,
                "customer-config",
                new { salesZone = "Sur" }
            )
        )
            .StatusCode.Should()
            .Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Cada_invariante_de_Domain_responde_400_BadRequest_con_el_mensaje_historico()
    {
        var bp = await CreateBusinessPartnerAsync("1791352688001", "QUALA ECUADOR S A");
        var supplier = await AssignAsync(bp, "Supplier");
        var carrier = await AssignAsync(bp, "Carrier");
        var customer = await AssignAsync(bp, "Customer");

        var cases = new (string Config, string Role, object Body, Action Domain)[]
        {
            (
                "supplier-config",
                supplier,
                new { defaultTaxSupportCode = Long(6) },
                () => SupplierRoleConfig.Create(Long(6))
            ),
            (
                "supplier-config",
                supplier,
                new { defaultPaymentMethodCode = Long(6) },
                () => SupplierRoleConfig.Create(defaultPaymentMethodCode: Long(6))
            ),
            (
                "supplier-config",
                supplier,
                new { refundProviderTypeCode = Long(6) },
                () => SupplierRoleConfig.Create(refundProviderTypeCode: Long(6))
            ),
            (
                "carrier-config",
                carrier,
                new { transportAuthorizationNumber = Long(51) },
                () => CarrierRoleConfig.Create(Long(51))
            ),
            (
                "carrier-config",
                carrier,
                new { vehicleCapacityTons = 0m },
                () => CarrierRoleConfig.Create(vehicleCapacityTons: 0m)
            ),
            (
                "carrier-config",
                carrier,
                new { vehicleCapacityTons = -3m },
                () => CarrierRoleConfig.Create(vehicleCapacityTons: -3m)
            ),
            (
                "customer-config",
                customer,
                new { customerCategory = Long(51) },
                () => CustomerRoleConfig.Create(customerCategory: Long(51))
            ),
            (
                "customer-config",
                customer,
                new { customerSegment = Long(51) },
                () => CustomerRoleConfig.Create(customerSegment: Long(51))
            ),
            (
                "customer-config",
                customer,
                new { salesZone = Long(101) },
                () => CustomerRoleConfig.Create(salesZone: Long(101))
            ),
            (
                "customer-config",
                customer,
                new { creditRating = Long(11) },
                () => CustomerRoleConfig.Create(creditRating: Long(11))
            ),
            (
                "customer-config",
                customer,
                new { loyaltyTier = Long(21) },
                () => CustomerRoleConfig.Create(loyaltyTier: Long(21))
            ),
            (
                "customer-config",
                customer,
                new { preferredInvoiceFormat = Long(21) },
                () => CustomerRoleConfig.Create(preferredInvoiceFormat: Long(21))
            ),
            (
                "customer-config",
                customer,
                new { customerClassification = Long(51) },
                () => CustomerRoleConfig.Create(customerClassification: Long(51))
            ),
        };

        foreach (var (config, role, body, domain) in cases)
        {
            var because = $"{config} {JsonSerializer.Serialize(body)}";
            await AssertInvariantResponse(
                await Patch(bp, role, config, body),
                DomainMessage(domain),
                because
            );
        }

        // Assign: mismo contrato (antes salía por ExceptionMiddleware con el ArgumentException del VO).
        var otherBp = await CreateBusinessPartnerAsync("1791352688005", "Otra Empresa");
        await AssertInvariantResponse(
            await _client.PostAsJsonAsync(
                Roles(otherBp),
                new { roleType = "Carrier", carrierConfig = new { vehicleCapacityTons = 0m } }
            ),
            DomainMessage(() => CarrierRoleConfig.Create(vehicleCapacityTons: 0m)),
            "assign carrier"
        );
        await AssertInvariantResponse(
            await _client.PostAsJsonAsync(
                Roles(otherBp),
                new
                {
                    roleType = "Supplier",
                    supplierConfig = new { defaultTaxSupportCode = Long(6) },
                }
            ),
            DomainMessage(() => SupplierRoleConfig.Create(Long(6))),
            "assign supplier"
        );
        (await _client.GetFromJsonAsync<JsonElement>(Roles(otherBp)))
            .GetProperty("data")
            .GetArrayLength()
            .Should()
            .Be(0, "un invariante violado no crea el rol");
    }

    [Fact]
    public async Task Rol_de_otro_BP_responde_404_identico_a_rol_inexistente()
    {
        var bpA = await CreateBusinessPartnerAsync("1791352688001", "Empresa A");
        var bpB = await CreateBusinessPartnerAsync("1791352688005", "Empresa B");
        var roleOfB = await AssignAsync(bpB, "Supplier");

        foreach (
            var (config, body) in new (string, object)[]
            {
                ("supplier-config", new { isRetentionExempt = true }),
                ("carrier-config", new { vehicleCapacityTons = 5m }),
                ("customer-config", new { salesZone = "Norte" }),
            }
        )
        {
            var crossParent = await Patch(bpA, roleOfB, config, body);
            var missing = await Patch(bpA, Guid.NewGuid().ToString(), config, body);

            crossParent.StatusCode.Should().Be(HttpStatusCode.NotFound, config);
            missing.StatusCode.Should().Be(HttpStatusCode.NotFound, config);
            var a = JsonDocument.Parse(await crossParent.Content.ReadAsStringAsync()).RootElement;
            var b = JsonDocument.Parse(await missing.Content.ReadAsStringAsync()).RootElement;
            a.GetProperty("code").GetString().Should().Be(b.GetProperty("code").GetString());
            a.GetProperty("data")
                .GetRawText()
                .Should()
                .Be(b.GetProperty("data").GetRawText(), config);
        }
    }

    [Fact]
    public async Task Config_invalida_responde_400_antes_de_resolver_el_rol_exista_o_no()
    {
        var bp = await CreateBusinessPartnerAsync("1791352688001", "Empresa A");
        var expected = DomainMessage(() => CarrierRoleConfig.Create(vehicleCapacityTons: 0m));

        await AssertInvariantResponse(
            await Patch(
                bp,
                Guid.NewGuid().ToString(),
                "carrier-config",
                new { vehicleCapacityTons = 0m }
            ),
            expected,
            "rol inexistente"
        );
    }
}
