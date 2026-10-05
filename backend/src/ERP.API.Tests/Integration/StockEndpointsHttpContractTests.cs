using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ERP.API.Tests.Support;
using ERP.Application.Access.Authorization;
using ERP.Application.Common;
using ERP.Domain.Access.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Integration;

/// <summary>
/// ZH-API-THIN-STOCK-01 — contrato HTTP de los 13 endpoints de <c>/api/v1/inventory/stock</c>,
/// independiente del controller que los aloje (corre igual antes y después de separar consultas /
/// ajustes / transferencias): host real (routing, [Authorize], model binding, ApiResult) con un
/// IMediator que captura el request. Verifica el command/query exacto que llega a Application,
/// su marcador de scope Company/Branch (lo que aplican los behaviors: la separación no puede
/// saltarse el aislamiento), y los status de éxito/fallo.
/// El flujo funcional real de ajustes (válido, inválido, permisos 403) lo cubre
/// InventoryAdjustmentsEndToEndTests sobre estas mismas rutas.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class StockEndpointsHttpContractTests : IAsyncLifetime
{
    private readonly PostgreSqlTestWebAppFactory _factory = new();
    private readonly Guid _adminId = Guid.NewGuid();
    private Guid _tenantId;
    private Guid _companyId;
    private readonly List<object> _sent = [];
    private Func<object, object> _reply = SuccessFor;
    private WebApplicationFactory<Program> _app = null!;
    private HttpClient _client = null!;

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
            var tenant = Tenant.Create("ZH-Stock", $"zh-{Guid.NewGuid():N}", _adminId);
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

        _app = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IMediator>(
                    new StubMediator(request =>
                    {
                        _sent.Add(request);
                        return _reply(request);
                    })
                );
                services.AddScoped<IRuntimePermissionAuthorizer, AllowAllPermissionAuthorizer>();
            })
        );
        _client = _app.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtFactory.CreateSessionJwt(_tenantId, _adminId, _companyId, "Admin")
        );
        _factory.MutableTenant.TenantId = _tenantId;
        _factory.MutableCompany.CompanyId = _companyId;
        _factory.MutableUser.UserId = _adminId;
    }

    public async Task DisposeAsync()
    {
        await _app.DisposeAsync();
        await _factory.DisposeAsync();
    }

    // ── Respuestas del mediator stub, tipadas según el request ───────────────

    private static Type ResultType(object request) =>
        request
            .GetType()
            .GetInterfaces()
            .Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))
            .GetGenericArguments()[0];

    private static object SuccessFor(object request) =>
        ResultType(request).GetMethod("Success")!.Invoke(null, [null, null])!;

    private static object FailureFor(object request, string factory) =>
        factory == nameof(Result<object>.NotFound)
            ? ResultType(request).GetMethod(factory)!.Invoke(null, ["No encontrado."])!
            : ResultType(request)
                .GetMethod(factory)!
                .Invoke(null, ["Inválido.", ApiResponseCodes.Common.ValidationError])!;

    // ── Catálogo de los 13 endpoints ─────────────────────────────────────────

    private static readonly Guid Item = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Warehouse = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Doc = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly object AdjustmentBody = new
    {
        warehouseId = Warehouse,
        warehouseName = "Bodega",
        movementType = "PositiveAdjust",
        reasonId = Guid.NewGuid(),
        notes = "n",
        lines = Array.Empty<object>(),
    };

    public static TheoryData<string, string, string, int, string> Endpoints =>
        new()
        {
            // método, ruta, request esperado, status éxito, marcador de scope
            {
                "GET",
                $"/api/v1/inventory/stock?itemId={Item}&warehouseId={Warehouse}",
                "GetStockQuery",
                200,
                "Branch"
            },
            {
                "GET",
                $"/api/v1/inventory/stock/report?warehouseId={Warehouse}&search=x",
                "GetCurrentStockReportQuery",
                200,
                "Branch"
            },
            {
                "GET",
                $"/api/v1/inventory/stock/movements?itemId={Item}&warehouseId={Warehouse}&from=2026-01-01&to=2026-01-31",
                "GetStockMovementsQuery",
                200,
                "Branch"
            },
            {
                "GET",
                $"/api/v1/inventory/stock/aggregated/{Item}",
                "GetAggregatedStockQuery",
                200,
                "Company"
            },
            {
                "GET",
                $"/api/v1/inventory/stock/items/{Item}/warehouse-availability",
                "GetItemWarehouseAvailabilityQuery",
                200,
                "Branch"
            },
            {
                "GET",
                $"/api/v1/inventory/stock/adjustments?warehouseId={Warehouse}&status=Draft&pageNumber=2&pageSize=5",
                "ListStockAdjustmentsQuery",
                200,
                "Branch"
            },
            {
                "GET",
                $"/api/v1/inventory/stock/adjustments/{Doc}",
                "GetStockAdjustmentByIdQuery",
                200,
                "Branch"
            },
            {
                "POST",
                "/api/v1/inventory/stock/adjustments",
                "CreateStockAdjustmentCommand",
                201,
                "Branch"
            },
            {
                "PUT",
                $"/api/v1/inventory/stock/adjustments/{Doc}",
                "UpdateStockAdjustmentCommand",
                200,
                "Branch"
            },
            {
                "POST",
                $"/api/v1/inventory/stock/adjustments/{Doc}/execute",
                "ExecuteStockAdjustmentCommand",
                200,
                "Branch"
            },
            {
                "POST",
                $"/api/v1/inventory/stock/adjustments/{Doc}/cancel",
                "CancelStockAdjustmentCommand",
                200,
                "Branch"
            },
            {
                "POST",
                "/api/v1/inventory/stock/transfers",
                "CreateStockTransferCommand",
                201,
                "InterBranch"
            },
            {
                "POST",
                $"/api/v1/inventory/stock/transfers/{Doc}/confirm",
                "ConfirmStockTransferCommand",
                200,
                "InterBranch"
            },
        };

    private Task<HttpResponseMessage> Send(string method, string url)
    {
        object? body = url switch
        {
            "/api/v1/inventory/stock/adjustments" => AdjustmentBody,
            _ when method == "PUT" => new
            {
                id = Doc,
                warehouseId = Warehouse,
                warehouseName = "Bodega",
                movementType = "PositiveAdjust",
                reasonId = Guid.NewGuid(),
                notes = "n",
                lines = Array.Empty<object>(),
            },
            _ when url.EndsWith("/cancel", StringComparison.Ordinal) => new
            {
                reason = "Error de digitación",
            },
            "/api/v1/inventory/stock/transfers" => new
            {
                sourceWarehouseId = Warehouse,
                targetWarehouseId = Guid.NewGuid(),
                reason = "r",
                notes = "n",
                lines = new[]
                {
                    new
                    {
                        productId = Item,
                        quantity = 2.5m,
                        description = "Item",
                    },
                },
            },
            _ => method == "POST" ? new { } : null,
        };
        var message = new HttpRequestMessage(new HttpMethod(method), url);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return _client.SendAsync(message);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_envia_el_request_esperado_con_su_scope_y_responde_el_status_de_exito(
        string method,
        string url,
        string request,
        int status,
        string scope
    )
    {
        _sent.Clear();

        var response = await Send(method, url);

        ((int)response.StatusCode).Should().Be(status, await response.Content.ReadAsStringAsync());
        var sent = _sent.Should().ContainSingle().Subject;
        sent.GetType().Name.Should().Be(request);
        var marker = scope switch
        {
            "Branch" => typeof(IBranchScopedRequest),
            "InterBranch" => typeof(IInterBranchOperationRequest),
            _ => typeof(ICompanyScopedRequest),
        };
        sent.Should()
            .BeAssignableTo(
                marker,
                "el aislamiento Company/Branch lo aplican los behaviors según este marcador"
            );
    }

    [Fact]
    public async Task Los_parametros_de_ruta_query_y_body_llegan_intactos_a_Application()
    {
        _sent.Clear();

        await Send(
            "GET",
            $"/api/v1/inventory/stock/movements?itemId={Item}&warehouseId={Warehouse}&from=2026-01-01&to=2026-01-31"
        );
        await Send(
            "GET",
            $"/api/v1/inventory/stock/adjustments?warehouseId={Warehouse}&status=Draft&pageNumber=2&pageSize=5"
        );
        await Send("POST", $"/api/v1/inventory/stock/adjustments/{Doc}/cancel");
        await Send("POST", "/api/v1/inventory/stock/transfers");
        await Send("POST", $"/api/v1/inventory/stock/transfers/{Doc}/confirm");

        var json = _sent
            .Select(r => System.Text.Json.JsonSerializer.Serialize(r, r.GetType()))
            .ToList();
        json[0]
            .Should()
            .Be(
                $"{{\"ItemId\":\"{Item}\",\"WarehouseId\":\"{Warehouse}\",\"From\":\"2026-01-01\",\"To\":\"2026-01-31\"}}"
            );
        json[1]
            .Should()
            .Contain($"\"WarehouseId\":\"{Warehouse}\"")
            .And.Contain("\"Status\":\"Draft\"")
            .And.Contain("\"PageNumber\":2")
            .And.Contain("\"PageSize\":5");
        json[2].Should().Be($"{{\"Id\":\"{Doc}\",\"Reason\":\"Error de digitaci\\u00F3n\"}}");
        json[3]
            .Should()
            .Contain($"\"SourceWarehouseId\":\"{Warehouse}\"")
            .And.Contain($"\"ProductId\":\"{Item}\"")
            .And.Contain("\"Quantity\":2.5");
        json[4].Should().Be($"{{\"Id\":\"{Doc}\"}}");
    }

    [Theory]
    [InlineData(
        "POST",
        "/api/v1/inventory/stock/adjustments",
        "ValidationFailure",
        HttpStatusCode.UnprocessableEntity
    )]
    [InlineData(
        "POST",
        "/api/v1/inventory/stock/transfers",
        "ValidationFailure",
        HttpStatusCode.UnprocessableEntity
    )]
    [InlineData(
        "POST",
        "/api/v1/inventory/stock/transfers/33333333-3333-3333-3333-333333333333/confirm",
        "ValidationFailure",
        HttpStatusCode.UnprocessableEntity
    )]
    [InlineData(
        "POST",
        "/api/v1/inventory/stock/adjustments/33333333-3333-3333-3333-333333333333/execute",
        "ValidationFailure",
        HttpStatusCode.UnprocessableEntity
    )]
    [InlineData(
        "GET",
        "/api/v1/inventory/stock/adjustments/33333333-3333-3333-3333-333333333333",
        "NotFound",
        HttpStatusCode.NotFound
    )]
    public async Task Fallo_de_Application_conserva_su_status(
        string method,
        string url,
        string failure,
        HttpStatusCode expected
    )
    {
        _reply = request => FailureFor(request, failure);

        var response = await Send(method, url);

        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task Update_con_id_de_ruta_distinto_al_del_cuerpo_responde_400_sin_llamar_a_Application()
    {
        _sent.Clear();
        var message = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/v1/inventory/stock/adjustments/{Guid.NewGuid()}"
        )
        {
            Content = JsonContent.Create(
                new
                {
                    id = Doc,
                    warehouseId = Warehouse,
                    warehouseName = "Bodega",
                    movementType = "PositiveAdjust",
                    reasonId = Guid.NewGuid(),
                    notes = "n",
                    lines = Array.Empty<object>(),
                }
            ),
        };

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Sin_autenticacion_todos_los_endpoints_responden_401()
    {
        var anonymous = _app.CreateClient();
        foreach (var row in Endpoints)
        {
            var (method, url) = ((string)row[0], (string)row[1]);
            var response = await anonymous.SendAsync(
                new HttpRequestMessage(new HttpMethod(method), url)
            );
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"{method} {url}");
        }
    }
}
