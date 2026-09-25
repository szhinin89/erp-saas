using ERP.API.Tests.Support;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.MasterData.Constants;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Seeding.Steps;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ERP.API.Tests.Integration;

/// <summary>
/// Fase 5 — Validación funcional end-to-end del flujo completo Sucursal → Abrir Caja → Ventas →
/// Autorización, contra PostgreSQL real (Testcontainers) y el <see cref="Program"/> real completo
/// (todo el pipeline HTTP: controllers, MediatR, BranchScopeBehavior/IBranchAccessGuard,
/// FluentValidation, EF Core). No es una auditoría estática — ejercita las rutas HTTP reales que
/// usaría el frontend, exactamente como se plantea en el escenario de la Fase 5.
///
/// Sigue el mismo patrón que <see cref="Ride.RideControllerIntegrationTests"/>
/// (PostgreSqlTestWebAppFactory + TestJwtFactory), reutilizado tal cual — no se introduce
/// infraestructura de test nueva.
/// </summary>
public sealed class CajaVentasFlowFixture : IAsyncLifetime
{
    private readonly PostgreSqlTestWebAppFactory _baseFactory = new();

    public Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory => _baseFactory;
    public HttpClient Client { get; private set; } = null!;

    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid BranchAId { get; private set; }
    public Guid BranchBId { get; private set; }
    public Guid EmissionPointId { get; private set; }
    public string EmissionPointCode { get; private set; } = null!;
    public Guid CashRegisterA1Id { get; private set; }
    public Guid CashRegisterA2Id { get; private set; }
    public Guid CashRegisterA3Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid PaymentMethodId { get; private set; }
    public string VatCode { get; private set; } = null!;
    public decimal VatPercentage { get; private set; }

    private Guid _adminId;

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("JWT__SECRETKEY", IntegrationTestConstants.JwtSecretKey);
        Environment.SetEnvironmentVariable("JWT__ISSUER", "ZHTechnologies");
        Environment.SetEnvironmentVariable("JWT__AUDIENCE", "ERPUsers");

        await _baseFactory.InitializeAsync();
        await _baseFactory.MigrateAsync();
        await SeedAsync();

        Client = Factory.CreateClient();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtFactory.CreateSessionJwt(TenantId, Guid.NewGuid())
        );

        _baseFactory.MutableTenant.TenantId = TenantId;
        _baseFactory.MutableCompany.CompanyId = CompanyId;
    }

    public async Task DisposeAsync() => await Factory.DisposeAsync();

    private async Task SeedAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        _adminId = Guid.NewGuid();
        var tenant = Tenant.Create("ZH-CajaVentas-Test", $"zh-cv-{Guid.NewGuid():N}", _adminId);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        TenantId = tenant.Id;

        var company = Company.CreateManaged(
            TenantId,
            taxIdentificationNumber: $"179{TenantId:N}"[..13],
            legalName: "Empresa CajaVentas S.A.",
            createdBy: _adminId
        );
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        CompanyId = company.Id;

        var branchA = Branch.Create(
            tenantId: TenantId,
            name: "Quito Norte",
            address: "Av. Principal 123",
            code: "SUC-A",
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
            createdBy: _adminId,
            companyId: CompanyId
        );
        var branchB = Branch.Create(
            tenantId: TenantId,
            name: "Guayaquil Centro",
            address: "Av. 9 de Octubre 456",
            code: "SUC-B",
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
            isMainBranch: false,
            createdBy: _adminId,
            companyId: CompanyId
        );
        db.Branches.AddRange(branchA, branchB);
        await db.SaveChangesAsync();
        BranchAId = branchA.Id;
        BranchBId = branchB.Id;

        var establishment = Establishment.Create(
            TenantId,
            branchA.Id,
            CompanyId,
            "001",
            "Matriz",
            "Av. Principal 123",
            null,
            true,
            _adminId
        );
        db.Establishments.Add(establishment);
        await db.SaveChangesAsync();

        // Física: evita ejercitar el pipeline electrónico completo (SOAP SRI real) — fuera de
        // alcance de esta validación de integración Caja↔Ventas.
        var emissionPoint = EmissionPoint.Create(
            TenantId,
            CompanyId,
            establishment.Id,
            "001",
            null,
            EmissionType.Physical,
            true,
            _adminId
        );
        db.EmissionPoints.Add(emissionPoint);
        await db.SaveChangesAsync();
        EmissionPointId = emissionPoint.Id;
        EmissionPointCode = emissionPoint.Code;

        var registerA1 = CashRegister.Create(
            TenantId,
            CompanyId,
            BranchAId,
            "CAJA-01",
            "Caja Principal",
            _adminId,
            EmissionPointId
        );
        var registerA2 = CashRegister.Create(
            TenantId,
            CompanyId,
            BranchAId,
            "CAJA-02",
            "Caja Secundaria",
            _adminId,
            EmissionPointId
        );
        var registerA3 = CashRegister.Create(
            TenantId,
            CompanyId,
            BranchAId,
            "CAJA-03",
            "Caja Terciaria",
            _adminId,
            EmissionPointId
        );
        db.CashRegisters.AddRange(registerA1, registerA2, registerA3);
        await db.SaveChangesAsync();
        CashRegisterA1Id = registerA1.Id;
        CashRegisterA2Id = registerA2.Id;
        CashRegisterA3Id = registerA3.Id;

        // Código "CONTADO" (PaymentTermCodes.Cash) — es el código canónico que
        // SalesCreditRequirementPolicy.GetCashFallbackAsync busca vía GetByCodeAsync para el
        // fallback de ventas de contado (SALES-SETTLEMENT-CREDIT-01); un código distinto (p.ej.
        // "CONT") no es encontrado y la venta falla con VALIDATION_ERROR.
        var paymentTerm = PaymentTerm.Create(
            TenantId,
            PaymentTermCodes.Cash,
            "Contado",
            installments: 1,
            daysBetweenInstallments: 0,
            createdBy: _adminId
        );
        db.PaymentTerms.Add(paymentTerm);

        var paymentMethod = PaymentMethod.Create(
            TenantId,
            "01",
            "Efectivo",
            requiresReference: false,
            isCreditAllowed: false,
            sortOrder: 1,
            createdBy: _adminId,
            affectsPhysicalCash: true
        );
        db.PaymentMethods.Add(paymentMethod);
        await db.SaveChangesAsync();
        PaymentMethodId = paymentMethod.Id;

        var customer = BusinessPartner.Create(
            TenantId,
            "05",
            "1710034065",
            1,
            "Cliente E2E",
            _adminId
        );
        db.BusinessPartners.Add(customer);
        await db.SaveChangesAsync();
        CustomerId = customer.Id;
        db.BusinessPartnerRoles.Add(
            BusinessPartnerRole.Create(TenantId, customer.Id, RoleType.Customer, _adminId)
        );

        // Catálogo SRI (global, no tenant-scoped): el arranque de la app ya siembra códigos
        // reales (confirmado empíricamente — colisión de PK con el código "10"). Se usa un código
        // sintético que no coincide con ningún código oficial (numéricos cortos: "0","2","4"...)
        // para no depender de qué tarifas reales existan.
        VatCode = "TSTE2";
        VatPercentage = 15m;
        db.SriVatRates.Add(
            new SriVatRate
            {
                Code = VatCode,
                Name = "IVA Test 15%",
                Percentage = VatPercentage,
                IsActive = true,
            }
        );

        await db.SaveChangesAsync();

        // API-TESTS-CAJA-VENTAS-PAYMENT-TERM-SEED-01: este fixture crea Tenant/Company "a mano"
        // (arriba) en vez de pasar por CompanyProvisioningService, así que nunca corría el
        // bootstrap contable oficial (AccountingBootstrapStep: Plan de Cuentas + PostingRule
        // mínimas — MinimalPostingRules). AuthorizeSalesInvoiceHandler es fail-closed (nunca
        // autoriza sin asiento contabilizado — ver AuthorizeSalesUseCases.cs): sin la PostingRule
        // ("Sales","InvoiceIssued") sembrada, autorizar cualquier factura fallaba con
        // RULE_NOT_FOUND. Mismo patrón ya aplicado en SalesReturnFlowFixture
        // (API-TESTS-POSTING-RULE-NOT-FOUND-01) — se invoca aquí el mismo seeding oficial que
        // CompanyProvisioningService dispara en producción, nunca una PostingRule suelta
        // hardcodeada en el fixture.
        var accountingBootstrap = new AccountingBootstrapStep(
            db,
            new AlwaysTodayCompanyClock(),
            NullLogger<AccountingBootstrapStep>.Instance
        );
        await accountingBootstrap.ExecuteAsync(
            new CompanyBootstrapContext(TenantId, CompanyId, _adminId)
        );

        // API-TESTS-CAJA-VENTAS-PRECISION-POLICY-SEED-01: mismo motivo — sin el bootstrap oficial
        // de CompanyPrecisionPolicy (paso que CompanyProvisioningService ejecuta en producción),
        // crear ventas falla fail-closed con CompanyPrecisionPolicyMissingException.
        await new PrecisionPolicyBootstrapStep(db).ExecuteAsync(
            new CompanyBootstrapContext(TenantId, CompanyId, _adminId)
        );
    }

    /// <summary>
    /// Crea un usuario real con membresía Admin y acceso autorizado a las sucursales indicadas
    /// (CompanyUserBranch). Cada test usa su propio usuario para no compartir el invariante
    /// "una sesión de caja abierta por usuario" entre pruebas independientes.
    /// </summary>
    public async Task<Guid> CreateUserWithBranchAccessAsync(params Guid[] branchIds)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var user = IdentityUser.Create(
            $"cajero-{Guid.NewGuid():N}",
            "Cajero",
            "E2E",
            $"cajero-{Guid.NewGuid():N}@example.com",
            "hash",
            _adminId
        );
        db.IdentityUsers.Add(user);
        await db.SaveChangesAsync();

        var membership = CompanyUserMembership.Create(CompanyId, user.Id, "Admin", null, _adminId);
        db.CompanyUserMemberships.Add(membership);
        await db.SaveChangesAsync();

        foreach (var branchId in branchIds)
            db.CompanyUserBranches.Add(
                CompanyUserBranch.Create(TenantId, CompanyId, membership.Id, branchId, _adminId)
            );
        await db.SaveChangesAsync();

        return user.Id;
    }

    /// <summary>Fija la identidad operativa (usuario) y la sucursal activa (header real) para las siguientes llamadas HTTP.</summary>
    public void SetActiveContext(Guid userId, Guid branchId)
    {
        _baseFactory.MutableUser.UserId = userId;
        Client.DefaultRequestHeaders.Remove("X-Branch-Id");
        Client.DefaultRequestHeaders.Add("X-Branch-Id", branchId.ToString());
    }

    /// <summary>
    /// TREASURY-CASH-MANUAL-MOVEMENTS-PERMISSION-06 — variante con rol/perfil granular (mismo
    /// patrón que InventoryAdjustmentsFlowFixture.CreateUserWithBranchAccessAsync(role, profileId)):
    /// si <paramref name="profileId"/> es null, el rol pasado gobierna (Admin bypasea todo perm:
    /// check); si se especifica, la membresía queda ligada a ese AccessProfile (permisos granulares).
    /// </summary>
    public async Task<Guid> CreateUserWithBranchAccessAsync(
        string role,
        Guid? profileId,
        params Guid[] branchIds
    )
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var user = IdentityUser.Create(
            $"user-{Guid.NewGuid():N}",
            "Usuario",
            "E2E",
            $"user-{Guid.NewGuid():N}@example.com",
            "hash",
            _adminId
        );
        db.IdentityUsers.Add(user);
        await db.SaveChangesAsync();

        var membership = CompanyUserMembership.Create(CompanyId, user.Id, role, profileId, _adminId);
        db.CompanyUserMemberships.Add(membership);
        await db.SaveChangesAsync();

        foreach (var branchId in branchIds)
            db.CompanyUserBranches.Add(
                CompanyUserBranch.Create(TenantId, CompanyId, membership.Id, branchId, _adminId)
            );
        await db.SaveChangesAsync();

        return user.Id;
    }

    /// <summary>Crea un AccessProfile con exactamente los permisos indicados (IsAllowed=true) — mismo patrón que InventoryAdjustmentsFlowFixture.</summary>
    public async Task<Guid> CreateProfileWithPermissionsAsync(string name, params string[] permissionKeys)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var profile = AccessProfile.Create(TenantId, name, null, _adminId);
        db.AccessProfiles.Add(profile);
        await db.SaveChangesAsync();

        foreach (var key in permissionKeys)
            db.AccessProfilePermissions.Add(
                AccessProfilePermission.Create(TenantId, profile.Id, key, true, _adminId)
            );
        await db.SaveChangesAsync();

        return profile.Id;
    }

    /// <summary>
    /// Cliente HTTP dedicado a un usuario/rol distinto del admin del fixture — mismo mecanismo que
    /// InventoryAdjustmentsFlowFixture.CreateClientForUser (nuevo HttpClient con su propio JWT +
    /// header de sucursal, y actualiza el ICurrentUser mutable compartido).
    /// </summary>
    public HttpClient CreateClientForUser(Guid userId, string role, Guid branchId)
    {
        _baseFactory.MutableUser.UserId = userId;
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtFactory.CreateSessionJwt(TenantId, userId, role: role)
        );
        client.DefaultRequestHeaders.Add("X-Branch-Id", branchId.ToString());
        return client;
    }

    /// <summary>Crea/aplica un OrgSetting Company-scope real (mismo mecanismo que usa UpdateOperationalPreferencesCommandHandler) — no un mock.</summary>
    public async Task SetOrgSettingAsync(string key, string value, SettingDataType dataType)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        db.Set<OrgSetting>()
            .Add(OrgSetting.Create(TenantId, CompanyId, OrgScope.Company, CompanyId, key, value, dataType, _adminId));
        await db.SaveChangesAsync();
    }

    /// <summary>Motivo de movimiento manual válido (ManualIncome) para esta empresa — catálogo real, sin mocks.</summary>
    public async Task<Guid> CreateManualIncomeReasonAsync(string code)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var reason = CashMovementReason.Create(
            TenantId, CompanyId, code, "Motivo E2E", CashMovementType.ManualIncome, 1, _adminId
        );
        db.Set<CashMovementReason>().Add(reason);
        await db.SaveChangesAsync();
        return reason.Id;
    }

    public IServiceScope CreateDbScope() => Factory.Services.CreateScope();
}

[Trait("Category", "PostgreSql")]
public sealed class CajaVentasEndToEndTests : IClassFixture<CajaVentasFlowFixture>
{
    private readonly CajaVentasFlowFixture _f;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public CajaVentasEndToEndTests(CajaVentasFlowFixture fixture) => _f = fixture;

    [Fact]
    public async Task Flujo_completo_Sucursal_Caja_Ventas_Autorizacion_end_to_end()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync(_f.BranchAId);
        _f.SetActiveContext(userId, _f.BranchAId);

        // ── 3. Abrir caja ────────────────────────────────────────────────
        var openResponse = await _f.Client.PostAsJsonAsync(
            "/api/v1/cash-sessions/open",
            new
            {
                cashRegisterId = _f.CashRegisterA1Id,
                openingAmount = 100m,
                notes = "Apertura E2E",
            }
        );

        openResponse
            .StatusCode.Should()
            .Be(HttpStatusCode.Created, await openResponse.Content.ReadAsStringAsync());
        var openBody = await openResponse.Content.ReadFromJsonAsync<
            Envelope<CashSessionResponseDto>
        >(JsonOptions);
        var session = openBody!.Data!;

        // Fase 6: CashSessionDto ahora expone BranchId (gap detectado en Fase 5).
        session.BranchId.Should().Be(_f.BranchAId);
        session.CashRegisterId.Should().Be(_f.CashRegisterA1Id);
        session.CashRegisterCodeSnapshot.Should().Be("CAJA-01");
        session.CashRegisterNameSnapshot.Should().Be("Caja Principal");
        session.EmissionPointId.Should().Be(_f.EmissionPointId);
        session.EmissionPointCodeSnapshot.Should().Be(_f.EmissionPointCode);
        session.OpeningAmount.Should().Be(100m);
        session.Status.Should().Be("Open");

        // ── GET /cash-sessions/my — contexto operativo ─────────────────────
        var myResponse = await _f.Client.GetAsync("/api/v1/cash-sessions/my");
        myResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var myBody = await myResponse.Content.ReadFromJsonAsync<Envelope<CashSessionResponseDto>>(
            JsonOptions
        );
        myBody!.Data!.Id.Should().Be(session.Id);

        // ── 5. Crear factura — el cliente NO envía EmissionPointId/CashSessionId/CashRegisterId ──
        const decimal unitPrice = 100m;
        var expectedVat = Math.Round(
            unitPrice * _f.VatPercentage / 100m,
            2,
            MidpointRounding.AwayFromZero
        );
        var expectedGrandTotal = unitPrice + expectedVat;

        var createResponse = await _f.Client.PostAsJsonAsync(
            "/api/v1/sales",
            new
            {
                customerId = _f.CustomerId,
                issueDate = DateOnly
                    .FromDateTime(DateTime.UtcNow.AddDays(-1))
                    .ToString("yyyy-MM-dd"),
                lines = new[]
                {
                    new
                    {
                        description = "Producto E2E",
                        quantity = 1m,
                        unitPrice,
                        vatCode = _f.VatCode,
                    },
                },
                payments = new[]
                {
                    new { paymentMethodId = _f.PaymentMethodId, amount = expectedGrandTotal },
                },
            }
        );

        createResponse
            .StatusCode.Should()
            .Be(HttpStatusCode.Created, await createResponse.Content.ReadAsStringAsync());
        var createBody = await createResponse.Content.ReadFromJsonAsync<
            Envelope<SalesInvoiceResponseDto>
        >(JsonOptions);
        var invoice = createBody!.Data!;

        invoice
            .CashSessionId.Should()
            .Be(session.Id, "el servidor debe resolver CashSessionId desde ICurrentCashSession");
        invoice
            .EmissionPointId.Should()
            .Be(
                _f.EmissionPointId,
                "el servidor debe resolver EmissionPointId desde la CashSession activa"
            );
        invoice.Status.Should().Be("Draft");

        // ── 6. Autorizar factura ─────────────────────────────────────────
        var authorizeResponse = await _f.Client.PostAsync(
            $"/api/v1/sales/{invoice.Id}/authorize",
            null
        );
        authorizeResponse
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, await authorizeResponse.Content.ReadAsStringAsync());
        var authorizeBody = await authorizeResponse.Content.ReadFromJsonAsync<
            Envelope<SalesInvoiceResponseDto>
        >(JsonOptions);
        var authorized = authorizeBody!.Data!;

        authorized.Status.Should().Be("Authorized");
        authorized
            .EmissionPointId.Should()
            .Be(
                _f.EmissionPointId,
                "el SRI/numeración debe usar el punto de emisión de la caja, no uno distinto"
            );
        authorized.GrandTotal.Should().Be(expectedGrandTotal);

        // ── 7. Revisar datos persistidos directamente en BD ─────────────
        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var persistedSession = await db
            .CashSessions.AsNoTracking()
            .Include(s => s.Movements)
            .FirstAsync(s => s.Id == session.Id);
        persistedSession.BranchId.Should().Be(_f.BranchAId);
        persistedSession.CashRegisterId.Should().Be(_f.CashRegisterA1Id);
        persistedSession.EmissionPointId.Should().Be(_f.EmissionPointId);
        persistedSession.Status.Should().Be(CashSessionStatus.Open);
        persistedSession.OpeningAmount.Should().Be(100m);

        // Fase 6: corregido el bug de doble conteo de OpeningAmount detectado en Fase 5
        // (CashMovementType.Opening ya no se clasifica como "ingreso" en TotalIncome).
        persistedSession
            .CurrentBalance.Should()
            .Be(100m + expectedGrandTotal, "apertura + movimiento de venta");

        var persistedInvoice = await db
            .SalesInvoices.AsNoTracking()
            .FirstAsync(i => i.Id == invoice.Id);
        persistedInvoice.BranchId.Should().Be(_f.BranchAId);
        persistedInvoice.CashSessionId.Should().Be(session.Id);
        persistedInvoice.EmissionPointId.Should().Be(_f.EmissionPointId);

        var movement = persistedSession
            .Movements.Should()
            .ContainSingle(m => m.MovementType == CashMovementType.SaleIncome)
            .Subject;
        movement.Amount.Should().Be(expectedGrandTotal);
        movement.ReferenceId.Should().Be(invoice.Id);
    }

    [Fact]
    public async Task GetCashRegistersByCurrentBranch_solo_devuelve_cajas_de_la_sucursal_activa()
    {
        // Gap cerrado en Fase 6: en Fase 5 no existía este endpoint — se validaba abriendo caja
        // con un Id ya conocido de antemano, sin poder probar el listado real.
        var userId = await _f.CreateUserWithBranchAccessAsync(_f.BranchAId, _f.BranchBId);
        _f.SetActiveContext(userId, _f.BranchAId);

        var listResponse = await _f.Client.GetAsync("/api/v1/cash-registers");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = (
            await listResponse.Content.ReadFromJsonAsync<Envelope<List<CashRegisterResponseDto>>>(
                JsonOptions
            )
        )!.Data!;

        list.Should().OnlyContain(r => r.BranchId == _f.BranchAId);
        list.Select(r => r.Id)
            .Should()
            .Contain(new[] { _f.CashRegisterA1Id, _f.CashRegisterA2Id, _f.CashRegisterA3Id });

        // Cambiar el contexto activo a BranchB no debe devolver las cajas de BranchA.
        _f.SetActiveContext(userId, _f.BranchBId);
        var listB = (
            await (await _f.Client.GetAsync("/api/v1/cash-registers")).Content.ReadFromJsonAsync<
                Envelope<List<CashRegisterResponseDto>>
            >(JsonOptions)
        )!.Data!;
        listB.Should().NotContain(r => r.Id == _f.CashRegisterA1Id);

        // GetById de una caja de otra sucursal (BranchA) mientras el contexto activo es BranchB
        // debe tratarse como no encontrada — nunca exponer su existencia fuera de su sucursal.
        var getByIdResponse = await _f.Client.GetAsync(
            $"/api/v1/cash-registers/{_f.CashRegisterA1Id}"
        );
        getByIdResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        _f.SetActiveContext(userId, _f.BranchAId);
        var getByIdOk = await _f.Client.GetAsync($"/api/v1/cash-registers/{_f.CashRegisterA1Id}");
        getByIdOk.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Abrir_caja_de_otra_sucursal_es_rechazada()
    {
        // El usuario está autorizado en AMBAS sucursales (para aislar la regla de negocio de
        // CashRegister.BranchId de la guarda genérica de autorización de sucursal).
        var userId = await _f.CreateUserWithBranchAccessAsync(_f.BranchAId, _f.BranchBId);
        _f.SetActiveContext(userId, _f.BranchBId);

        // CashRegisterA2 pertenece a BranchA, pero el contexto operativo activo es BranchB.
        var response = await _f.Client.PostAsJsonAsync(
            "/api/v1/cash-sessions/open",
            new { cashRegisterId = _f.CashRegisterA2Id, openingAmount = 50m }
        );

        response
            .StatusCode.Should()
            .Be(HttpStatusCode.UnprocessableEntity, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Cliente_no_puede_forzar_EmissionPointId_CashSessionId_CashRegisterId_via_JSON_extra()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync(_f.BranchAId);
        _f.SetActiveContext(userId, _f.BranchAId);

        var openResponse = await _f.Client.PostAsJsonAsync(
            "/api/v1/cash-sessions/open",
            new { cashRegisterId = _f.CashRegisterA3Id, openingAmount = 20m }
        );
        openResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var session = (
            await openResponse.Content.ReadFromJsonAsync<Envelope<CashSessionResponseDto>>(
                JsonOptions
            )
        )!.Data!;

        var bogusEmissionPointId = Guid.NewGuid();
        var bogusCashSessionId = Guid.NewGuid();
        var bogusCashRegisterId = Guid.NewGuid();

        const decimal unitPrice = 50m;
        var expectedGrandTotal =
            unitPrice
            + Math.Round(unitPrice * _f.VatPercentage / 100m, 2, MidpointRounding.AwayFromZero);

        // El cliente intenta enviar campos que CreateSalesDraftCommand ya no expone —
        // System.Text.Json los ignora silenciosamente al no existir la propiedad en el record.
        var createResponse = await _f.Client.PostAsJsonAsync(
            "/api/v1/sales",
            new
            {
                customerId = _f.CustomerId,
                issueDate = DateOnly
                    .FromDateTime(DateTime.UtcNow.AddDays(-1))
                    .ToString("yyyy-MM-dd"),
                emissionPointId = bogusEmissionPointId,
                cashSessionId = bogusCashSessionId,
                cashRegisterId = bogusCashRegisterId,
                lines = new[]
                {
                    new
                    {
                        description = "Producto E2E 2",
                        quantity = 1m,
                        unitPrice,
                        vatCode = _f.VatCode,
                    },
                },
                payments = new[]
                {
                    new { paymentMethodId = _f.PaymentMethodId, amount = expectedGrandTotal },
                },
            }
        );

        createResponse
            .StatusCode.Should()
            .Be(HttpStatusCode.Created, await createResponse.Content.ReadAsStringAsync());
        var invoice = (
            await createResponse.Content.ReadFromJsonAsync<Envelope<SalesInvoiceResponseDto>>(
                JsonOptions
            )
        )!.Data!;

        invoice.CashSessionId.Should().Be(session.Id).And.NotBe(bogusCashSessionId);
        invoice.EmissionPointId.Should().Be(_f.EmissionPointId).And.NotBe(bogusEmissionPointId);
    }

    /// <summary>
    /// TREASURY-CASH-MANUAL-MOVEMENTS-PERMISSION-06 — verifica el pipeline REAL de autorización
    /// granular para "caja.record" (no el bypass de Admin, ya cubierto implícitamente por los
    /// demás escenarios), y su independencia respecto de la configuración de empresa
    /// AllowManualInOutMovements (TREASURY-CASH-MANUAL-MOVEMENTS-COMPANY-SETTING-05): ambas reglas
    /// deben cumplirse — ninguna sustituye a la otra. Ejercita
    /// RuntimePermissionAuthorizer/PermissionHandler/EffectivePermissionKeysProvider reales contra
    /// PostgreSQL, mismo mecanismo que Escenario7c en InventoryAdjustmentsEndToEndTests — no
    /// introduce infraestructura de test nueva.
    /// </summary>
    [Fact]
    public async Task Registrar_movimiento_manual_exige_permiso_caja_record_y_configuracion_de_empresa()
    {
        // ── Turno abierto por un usuario con acceso total (Admin) — precondición común a todos
        // los sub-escenarios; lo que se prueba es la autorización de registrar el movimiento, no
        // la apertura del turno. ──
        var adminUserId = await _f.CreateUserWithBranchAccessAsync(_f.BranchAId);
        _f.SetActiveContext(adminUserId, _f.BranchAId);
        var openResponse = await _f.Client.PostAsJsonAsync(
            "/api/v1/cash-sessions/open",
            new { cashRegisterId = _f.CashRegisterA1Id, openingAmount = 100m, notes = (string?)null }
        );
        openResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var session = (
            await openResponse.Content.ReadFromJsonAsync<Envelope<CashSessionResponseDto>>(JsonOptions)
        )!.Data!;
        var reasonId = await _f.CreateManualIncomeReasonAsync("PERM06-INGRESO");

        object MovementBody() =>
            new
            {
                movementType = "ManualIncome",
                reasonId,
                amount = 15m,
                description = "Ingreso E2E permiso granular",
            };

        // ── 1) Empresa permite (default true) + usuario SIN caja.record → 403 ──────────────
        var noRecordProfileId = await _f.CreateProfileWithPermissionsAsync(
            "Solo Vista Caja",
            CajaPermissions.View
        );
        var noRecordUserId = await _f.CreateUserWithBranchAccessAsync(
            "Operador",
            noRecordProfileId,
            _f.BranchAId
        );
        var noRecordClient = _f.CreateClientForUser(noRecordUserId, "Operador", _f.BranchAId);

        var forbiddenResponse = await noRecordClient.PostAsJsonAsync(
            $"/api/v1/cash-sessions/{session.Id}/movements",
            MovementBody()
        );
        forbiddenResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // La lectura (view) sí debe funcionar para este usuario — el gate es específico de Record.
        var getAsViewOnly = await noRecordClient.GetAsync($"/api/v1/cash-sessions/{session.Id}");
        getAsViewOnly.StatusCode.Should().Be(HttpStatusCode.OK);

        // ── 2) Empresa permite (default true) + usuario CON caja.record → éxito ────────────
        var recordProfileId = await _f.CreateProfileWithPermissionsAsync(
            "Vista y Registro de Movimientos",
            CajaPermissions.View,
            CajaPermissions.Record
        );
        var recordUserId = await _f.CreateUserWithBranchAccessAsync(
            "Operador",
            recordProfileId,
            _f.BranchAId
        );
        var recordClient = _f.CreateClientForUser(recordUserId, "Operador", _f.BranchAId);

        var allowedResponse = await recordClient.PostAsJsonAsync(
            $"/api/v1/cash-sessions/{session.Id}/movements",
            MovementBody()
        );
        allowedResponse
            .StatusCode.Should()
            .Be(HttpStatusCode.Created, await allowedResponse.Content.ReadAsStringAsync());

        // ── 3) Empresa deshabilita AllowManualInOutMovements + mismo usuario CON caja.record
        // → rechazado por configuración (422, NO 403) — el permiso por sí solo no basta. ──────
        await _f.SetOrgSettingAsync(
            OrgSettingKeys.Cash.AllowManualInOutMovements,
            "false",
            SettingDataType.Bool
        );

        var rejectedByConfigResponse = await recordClient.PostAsJsonAsync(
            $"/api/v1/cash-sessions/{session.Id}/movements",
            MovementBody()
        );
        rejectedByConfigResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // Restaura el contexto de usuario mutable compartido al admin del fixture y cierra el
        // turno que abrió este test — CashRegisterA1Id es compartido por otros Facts de esta
        // clase (IClassFixture: mismo Postgres para todos), y "una caja abierta por registradora"
        // es un invariante real que bloquearía a los demás si esta sesión quedara abierta.
        _f.SetActiveContext(adminUserId, _f.BranchAId);
        var closeResponse = await _f.Client.PostAsJsonAsync(
            $"/api/v1/cash-sessions/{session.Id}/close",
            new
            {
                closingCounts = new[]
                {
                    new { denominationValue = 100m, denominationLabel = "$100", quantity = 1 },
                    new { denominationValue = 10m, denominationLabel = "$10", quantity = 1 },
                    new { denominationValue = 5m, denominationLabel = "$5", quantity = 1 },
                },
                closeNotes = (string?)null,
            }
        );
        closeResponse
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, await closeResponse.Content.ReadAsStringAsync());
    }
}

internal sealed record Envelope<T>(T? Data);

internal sealed record CashSessionResponseDto(
    Guid Id,
    Guid BranchId,
    Guid CashRegisterId,
    string CashRegisterCodeSnapshot,
    string CashRegisterNameSnapshot,
    Guid EmissionPointId,
    string EmissionPointCodeSnapshot,
    decimal OpeningAmount,
    string Status
);

internal sealed record SalesInvoiceResponseDto(
    Guid Id,
    Guid CashSessionId,
    Guid? EmissionPointId,
    string Status,
    decimal GrandTotal
);

internal sealed record CashRegisterResponseDto(
    Guid Id,
    Guid BranchId,
    Guid? EmissionPointId,
    string Code,
    string Name,
    bool IsActive
);
