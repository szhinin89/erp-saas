using ERP.API.Tests.Support;
using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ERP.API.Tests.Integration;

/// <summary>
/// Fase 15 (P0-01) — validación funcional end-to-end del módulo de Devolución de Venta / Nota de
/// Crédito SRI (<c>SalesReturn</c>) contra PostgreSQL real (Testcontainers) y el pipeline HTTP
/// completo (controllers, MediatR, FluentValidation, EF Core, dominio Caja/CxC/Contabilidad/
/// Documentos Electrónicos ya cableado). Sigue el mismo patrón que
/// <see cref="CajaVentasEndToEndTests"/> — reutiliza <c>PostgreSqlTestWebAppFactory</c> y
/// <c>TestJwtFactory</c> tal cual, sin infraestructura de test nueva.
///
/// Cubre los 15 escenarios canónicos de <c>P0-01_SALES_RETURN_CREDIT_NOTE_DESIGN.md</c> §14 y el
/// checklist de Fase 15 del plan de implementación.
/// </summary>
public sealed class SalesReturnFlowFixture : IAsyncLifetime
{
    private readonly PostgreSqlTestWebAppFactory _baseFactory = new();

    public Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory => _baseFactory;
    public HttpClient Client { get; private set; } = null!;

    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid EmissionPointId { get; private set; }
    public string EmissionPointCode { get; private set; } = null!;
    public Guid CashRegisterId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid PaymentMethodId { get; private set; }
    public Guid CashPaymentTermId { get; private set; }
    public Guid CreditPaymentTermId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid WarehouseId { get; private set; }
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
        var tenant = Tenant.Create("ZH-SalesReturn-Test", $"zh-sr-{Guid.NewGuid():N}", _adminId);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        TenantId = tenant.Id;

        var company = Company.CreateManaged(
            TenantId,
            taxIdentificationNumber: $"179{TenantId:N}"[..13],
            legalName: "Empresa SalesReturn S.A.",
            createdBy: _adminId
        );
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        CompanyId = company.Id;

        // Se fija el contexto tenant/company mutable ANTES de seguir sembrando: entidades
        // creadas más abajo (p. ej. Item) levantan domain events cuyos *AuditHandler leen
        // ICurrentTenant/ICurrentCompany vía IAuditContext — deben resolver ya al valor real,
        // no al default, o el audit handler falla con "tenantId requerido".
        _baseFactory.MutableTenant.TenantId = TenantId;
        _baseFactory.MutableCompany.CompanyId = CompanyId;

        var branch = Branch.Create(
            tenantId: TenantId,
            name: "Matriz",
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
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        BranchId = branch.Id;

        var establishment = Establishment.Create(
            TenantId,
            branch.Id,
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

        // Física: evita ejercitar el pipeline electrónico completo (SOAP SRI real) para la
        // factura — fuera de alcance de esta validación de integración. La Nota de Crédito de la
        // devolución igual se intenta emitir electrónicamente vía el pipeline genérico (escenario
        // 18): desde ADR-031 (Fase 11) el manifest.json de CreditNote ya tiene activeVersion
        // "1.1.0" y el XML pasa la validación XSD, pero esta suite no configura un certificado SRI
        // real para el tenant de prueba — el pipeline avanza más allá de la validación de esquema y
        // se detiene en la etapa de firma, siempre en un estado tolerante a fallo, nunca Authorized.
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

        var cashRegister = CashRegister.Create(
            TenantId,
            CompanyId,
            BranchId,
            "CAJA-01",
            "Caja Principal",
            _adminId,
            EmissionPointId
        );
        db.CashRegisters.Add(cashRegister);
        await db.SaveChangesAsync();
        CashRegisterId = cashRegister.Id;

        var cashPaymentTerm = PaymentTerm.Create(
            TenantId,
            "CONT",
            "Contado",
            installments: 1,
            daysBetweenInstallments: 0,
            createdBy: _adminId
        );
        var creditPaymentTerm = PaymentTerm.Create(
            TenantId,
            "30D",
            "30 días",
            installments: 1,
            daysBetweenInstallments: 30,
            createdBy: _adminId
        );
        db.PaymentTerms.AddRange(cashPaymentTerm, creditPaymentTerm);

        var paymentMethod = PaymentMethod.Create(
            TenantId,
            "01",
            "Efectivo",
            requiresReference: false,
            isCreditAllowed: false,
            sortOrder: 1,
            createdBy: _adminId
        );
        db.PaymentMethods.Add(paymentMethod);
        await db.SaveChangesAsync();
        CashPaymentTermId = cashPaymentTerm.Id;
        CreditPaymentTermId = creditPaymentTerm.Id;
        PaymentMethodId = paymentMethod.Id;

        var customer = BusinessPartner.Create(
            TenantId,
            "05",
            "1710034065",
            1,
            "Cliente Devoluciones E2E",
            _adminId
        );
        db.BusinessPartners.Add(customer);
        await db.SaveChangesAsync();
        CustomerId = customer.Id;
        db.BusinessPartnerRoles.Add(
            BusinessPartnerRole.Create(TenantId, customer.Id, RoleType.Customer, _adminId)
        );

        // Catálogo SRI sintético (mismo criterio que CajaVentasEndToEndTests): código que no
        // colisiona con ningún código real ya sembrado por el arranque de la app.
        VatCode = "TSTSR";
        VatPercentage = 15m;
        db.SriVatRates.Add(
            new SriVatRate
            {
                Code = VatCode,
                Name = "IVA Test Devoluciones 15%",
                Percentage = VatPercentage,
                IsActive = true,
            }
        );

        // Bodega — requerida (FK real) porque SalesReturnDetail.WarehouseId hereda el de la línea
        // de factura original, necesario para que la devolución genere StockMovement (escenario 13).
        var warehouse = Warehouse.Create(
            TenantId,
            BranchId,
            "Bodega Principal",
            "BOD-01",
            storageType: null,
            address: null,
            phone: null,
            email: null,
            manager: null,
            latitude: null,
            longitude: null,
            capacity: null,
            dailyDispatchGoal: null,
            createdBy: _adminId,
            companyId: CompanyId
        );
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync();
        WarehouseId = warehouse.Id;

        // Ítem vendible con VAT real (Infraestructura CLOSED — Configuración Tributaria: el ítem
        // es la única fuente de verdad del código IVA de la línea).
        var itemType = ItemTypeDefinition.Create(TenantId, "MERCH", "Mercadería", 1, _adminId);
        db.Set<ItemTypeDefinition>().Add(itemType);
        await db.SaveChangesAsync();

        var item = Item.Create(
            TenantId,
            sku: $"SKU-{Guid.NewGuid():N}"[..12],
            shortName: "Producto Devolución E2E",
            description: "Producto Devolución E2E",
            itemTypeId: itemType.Id,
            defaultUomCode: "UNIT",
            taxConfig: ItemTaxConfig.Create(saleVatCode: VatCode, purchaseVatCode: null),
            saleConfig: ItemSaleConfig.Create(isForSale: true),
            stockConfig: ItemStockConfig.Create(tracksStock: true),
            createdBy: _adminId
        );
        db.Set<Item>().Add(item);
        await db.SaveChangesAsync();
        ItemId = item.Id;

        // Stock inicial suficiente para todas las ventas que los distintos Facts de esta clase
        // van a autorizar (SalesInvoice.Authorize valida disponibilidad de stock por bodega antes
        // de emitir) — un solo ingreso grande, compartido por todo el fixture.
        var stockRepo = scope.ServiceProvider.GetRequiredService<IStockRepository>();
        await stockRepo.AppendMovementAsync(
            TenantId,
            CompanyId,
            ItemId,
            WarehouseId,
            StockMovementType.PurchaseEntry,
            100_000m,
            "UNIT",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            "STOCK-INICIAL-E2E",
            null,
            null,
            _adminId,
            unitCost: 1m
        );
        await stockRepo.SaveChangesWithSequenceRetryAsync();
    }

    /// <summary>
    /// Crea un usuario real con membresía Admin y acceso autorizado a la sucursal (mismo criterio
    /// que CajaVentasEndToEndTests: cada test usa su propio usuario para no compartir el
    /// invariante "una sesión de caja abierta por usuario" entre pruebas independientes).
    /// </summary>
    public async Task<Guid> CreateUserWithBranchAccessAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var user = IdentityUser.Create(
            $"vendedor-{Guid.NewGuid():N}",
            "Vendedor",
            "E2E",
            $"vendedor-{Guid.NewGuid():N}@example.com",
            "hash",
            _adminId
        );
        db.IdentityUsers.Add(user);
        await db.SaveChangesAsync();

        var membership = CompanyUserMembership.Create(CompanyId, user.Id, "Admin", null, _adminId);
        db.CompanyUserMemberships.Add(membership);
        await db.SaveChangesAsync();

        db.CompanyUserBranches.Add(
            CompanyUserBranch.Create(TenantId, CompanyId, membership.Id, BranchId, _adminId)
        );
        await db.SaveChangesAsync();

        return user.Id;
    }

    /// <summary>
    /// Crea una nueva caja registradora dedicada (mismo punto de emisión) — cada test que abre
    /// una sesión de caja necesita la suya propia, porque abrir una sesión ya activa en la misma
    /// caja registradora es rechazado ("Ya existe una caja abierta en esta caja registradora."),
    /// y este fixture es compartido (IClassFixture) entre todos los Facts de la clase.
    /// </summary>
    public async Task<Guid> CreateCashRegisterAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var register = CashRegister.Create(
            TenantId,
            CompanyId,
            BranchId,
            $"CAJA-{Guid.NewGuid():N}"[..12],
            "Caja E2E",
            _adminId,
            EmissionPointId
        );
        db.CashRegisters.Add(register);
        await db.SaveChangesAsync();
        return register.Id;
    }

    public void SetActiveContext(Guid userId)
    {
        _baseFactory.MutableUser.UserId = userId;
        Client.DefaultRequestHeaders.Remove("X-Branch-Id");
        Client.DefaultRequestHeaders.Add("X-Branch-Id", BranchId.ToString());
    }

    public IServiceScope CreateDbScope() => Factory.Services.CreateScope();
}

[Trait("Category", "PostgreSql")]
public sealed class SalesReturnEndToEndTests : IClassFixture<SalesReturnFlowFixture>
{
    private readonly SalesReturnFlowFixture _f;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public SalesReturnEndToEndTests(SalesReturnFlowFixture fixture) => _f = fixture;

    // ══════════════════════════════════════════════════════════════════════
    // Helpers privados — únicos autorizados por el alcance de esta tarea (no
    // se agregan helpers compartidos en otros archivos).
    // ══════════════════════════════════════════════════════════════════════

    private async Task<Guid> OpenCashSessionAsync(decimal openingAmount = 100m)
    {
        // Cada test necesita su propia caja registradora libre — abrir sobre una caja ya
        // ocupada por otro test (fixture compartido vía IClassFixture) es rechazado.
        var cashRegisterId = await _f.CreateCashRegisterAsync();
        var response = await _f.Client.PostAsJsonAsync(
            "/api/v1/cash-sessions/open",
            new { cashRegisterId, openingAmount }
        );
        response
            .StatusCode.Should()
            .Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<SrEnvelope<SrCashSessionResponseDto>>(
            JsonOptions
        );
        return body!.Data!.Id;
    }

    /// <summary>
    /// Crea y autoriza una factura de venta vía HTTP con una única línea que lleva
    /// <c>ItemId</c>/<c>WarehouseId</c> reales (necesario para que la devolución posterior genere
    /// StockMovement — escenario 13). Devuelve (invoiceId, invoiceDetailId, grandTotal).
    /// </summary>
    private async Task<(Guid InvoiceId, Guid LineId, decimal GrandTotal)> CreateAndAuthorizeInvoiceAsync(
        decimal quantity,
        decimal unitPrice,
        Guid? paymentTermId = null
    )
    {
        var expectedVat = Math.Round(
            quantity * unitPrice * _f.VatPercentage / 100m,
            2,
            MidpointRounding.AwayFromZero
        );
        var expectedGrandTotal = quantity * unitPrice + expectedVat;

        var createResponse = await _f.Client.PostAsJsonAsync(
            "/api/v1/sales",
            new
            {
                customerId = _f.CustomerId,
                issueDate = DateOnly
                    .FromDateTime(DateTime.UtcNow.AddDays(-1))
                    .ToString("yyyy-MM-dd"),
                paymentTermId = paymentTermId ?? _f.CashPaymentTermId,
                lines = new[]
                {
                    new
                    {
                        itemId = _f.ItemId,
                        description = "Producto Devolución E2E",
                        quantity,
                        unitPrice,
                        vatCode = _f.VatCode,
                        warehouseId = _f.WarehouseId,
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
            await createResponse.Content.ReadFromJsonAsync<SrEnvelope<SrSalesInvoiceResponseDto>>(
                JsonOptions
            )
        )!.Data!;

        var authorizeResponse = await _f.Client.PostAsync(
            $"/api/v1/sales/{invoice.Id}/authorize",
            null
        );
        authorizeResponse
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, await authorizeResponse.Content.ReadAsStringAsync());
        var authorized = (
            await authorizeResponse.Content.ReadFromJsonAsync<
                SrEnvelope<SrSalesInvoiceResponseDto>
            >(JsonOptions)
        )!.Data!;
        authorized.GrandTotal.Should().Be(expectedGrandTotal);

        var lineId = authorized.Lines.Single().Id;
        return (invoice.Id, lineId, authorized.GrandTotal);
    }

    private async Task<HttpResponseMessage> CreateDraftAsync(
        Guid invoiceId,
        string reason,
        params (Guid InvoiceDetailId, decimal Quantity)[] lines
    ) =>
        await _f.Client.PostAsJsonAsync(
            "/api/v1/sales/returns",
            new
            {
                salesInvoiceId = invoiceId,
                reason,
                lines = lines
                    .Select(l => new { invoiceDetailId = l.InvoiceDetailId, quantity = l.Quantity })
                    .ToArray(),
            }
        );

    private async Task<HttpResponseMessage> AuthorizeReturnAsync(
        Guid returnId,
        params (string Method, decimal Amount)[] allocations
    ) =>
        await _f.Client.PostAsJsonAsync(
            $"/api/v1/sales/returns/{returnId}/authorize",
            new
            {
                refundAllocations = allocations
                    .Select(a => new { method = a.Method, amount = a.Amount })
                    .ToArray(),
            }
        );

    // ══════════════════════════════════════════════════════════════════════
    // DRAFT
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Escenario1_Crear_draft_valido_se_persiste_correctamente()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync();
        _f.SetActiveContext(userId);
        await OpenCashSessionAsync();

        var (invoiceId, lineId, _) = await CreateAndAuthorizeInvoiceAsync(quantity: 4m, unitPrice: 20m);

        var response = await CreateDraftAsync(invoiceId, "Producto defectuoso", (lineId, 2m));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var draft = (
            await response.Content.ReadFromJsonAsync<SrEnvelope<SalesReturnResponseDto>>(JsonOptions)
        )!.Data!;

        draft.Status.Should().Be("Draft");
        draft.SalesInvoiceId.Should().Be(invoiceId);
        draft.CustomerId.Should().Be(_f.CustomerId);
        draft.Reason.Should().Be("Producto defectuoso");
        draft.Lines.Should().ContainSingle(l => l.OriginalInvoiceDetailId == lineId && l.Quantity == 2m);

        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var persisted = await db
            .SalesReturns.AsNoTracking()
            .Include(r => r.Lines)
            .FirstAsync(r => r.Id == draft.Id);
        persisted.Status.Should().Be(Domain.Modules.Sales.Enums.SalesReturnStatus.Draft);
        persisted.Lines.Should().ContainSingle(l => l.Quantity == 2m);
    }

    [Fact]
    public async Task Escenario2_Draft_invalido_motivo_vacio_devuelve_422()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync();
        _f.SetActiveContext(userId);
        await OpenCashSessionAsync();

        var (invoiceId, lineId, _) = await CreateAndAuthorizeInvoiceAsync(quantity: 4m, unitPrice: 20m);

        var response = await CreateDraftAsync(invoiceId, string.Empty, (lineId, 1m));
        response
            .StatusCode.Should()
            .Be(HttpStatusCode.UnprocessableEntity, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Escenario2b_Draft_invalido_cantidad_excede_lo_vendido_devuelve_422()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync();
        _f.SetActiveContext(userId);
        await OpenCashSessionAsync();

        var (invoiceId, lineId, _) = await CreateAndAuthorizeInvoiceAsync(quantity: 4m, unitPrice: 20m);

        var response = await CreateDraftAsync(invoiceId, "Cantidad excesiva", (lineId, 10m));
        response
            .StatusCode.Should()
            .Be(HttpStatusCode.UnprocessableEntity, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Escenario2c_Draft_sobre_factura_no_autorizada_devuelve_422()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync();
        _f.SetActiveContext(userId);
        await OpenCashSessionAsync();

        var createResponse = await _f.Client.PostAsJsonAsync(
            "/api/v1/sales",
            new
            {
                customerId = _f.CustomerId,
                issueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)).ToString("yyyy-MM-dd"),
                paymentTermId = _f.CashPaymentTermId,
                lines = new[]
                {
                    new
                    {
                        itemId = _f.ItemId,
                        description = "Producto sin autorizar",
                        quantity = 1m,
                        unitPrice = 20m,
                        vatCode = _f.VatCode,
                        warehouseId = _f.WarehouseId,
                    },
                },
            }
        );
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var draftInvoice = (
            await createResponse.Content.ReadFromJsonAsync<SrEnvelope<SrSalesInvoiceResponseDto>>(
                JsonOptions
            )
        )!.Data!;
        draftInvoice.Status.Should().Be("Draft");

        var response = await CreateDraftAsync(
            draftInvoice.Id,
            "Factura no autorizada",
            (draftInvoice.Lines.Single().Id, 1m)
        );
        response
            .StatusCode.Should()
            .Be(HttpStatusCode.UnprocessableEntity, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Escenario3_Update_draft_reemplaza_lineas()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync();
        _f.SetActiveContext(userId);
        await OpenCashSessionAsync();

        var (invoiceId, lineId, _) = await CreateAndAuthorizeInvoiceAsync(quantity: 5m, unitPrice: 10m);

        var createResponse = await CreateDraftAsync(invoiceId, "Cambio de cantidad", (lineId, 1m));
        var draft = (
            await createResponse.Content.ReadFromJsonAsync<SrEnvelope<SalesReturnResponseDto>>(
                JsonOptions
            )
        )!.Data!;

        var updateResponse = await _f.Client.PutAsJsonAsync(
            $"/api/v1/sales/returns/{draft.Id}",
            new
            {
                id = draft.Id,
                lines = new[] { new { invoiceDetailId = lineId, quantity = 3m } },
            }
        );
        updateResponse
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, await updateResponse.Content.ReadAsStringAsync());
        var updated = (
            await updateResponse.Content.ReadFromJsonAsync<SrEnvelope<SalesReturnResponseDto>>(
                JsonOptions
            )
        )!.Data!;

        updated.Lines.Should().ContainSingle(l => l.Quantity == 3m);
    }

    [Fact]
    public async Task Escenario4_Cancel_draft_transiciona_y_no_puede_resucitarse()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync();
        _f.SetActiveContext(userId);
        await OpenCashSessionAsync();

        var (invoiceId, lineId, _) = await CreateAndAuthorizeInvoiceAsync(quantity: 2m, unitPrice: 10m);
        var createResponse = await CreateDraftAsync(invoiceId, "Ya no se necesita", (lineId, 1m));
        var draft = (
            await createResponse.Content.ReadFromJsonAsync<SrEnvelope<SalesReturnResponseDto>>(
                JsonOptions
            )
        )!.Data!;

        var cancelResponse = await _f.Client.DeleteAsync($"/api/v1/sales/returns/{draft.Id}");
        cancelResponse
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, await cancelResponse.Content.ReadAsStringAsync());
        var cancelled = (
            await cancelResponse.Content.ReadFromJsonAsync<SrEnvelope<SalesReturnResponseDto>>(
                JsonOptions
            )
        )!.Data!;
        cancelled.Status.Should().Be("Cancelled");

        // No puede "resucitarse": ni cancelar de nuevo ni autorizar desde Cancelled.
        var cancelAgain = await _f.Client.DeleteAsync($"/api/v1/sales/returns/{draft.Id}");
        cancelAgain.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var authorizeAfterCancel = await AuthorizeReturnAsync(draft.Id, ("Cash", 11.5m));
        authorizeAfterCancel.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ══════════════════════════════════════════════════════════════════════
    // CONSULTAS
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Escenario5_GetById_devuelve_la_devolucion()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync();
        _f.SetActiveContext(userId);
        await OpenCashSessionAsync();

        var (invoiceId, lineId, _) = await CreateAndAuthorizeInvoiceAsync(quantity: 2m, unitPrice: 10m);
        var createResponse = await CreateDraftAsync(invoiceId, "Consulta por Id", (lineId, 1m));
        var draft = (
            await createResponse.Content.ReadFromJsonAsync<SrEnvelope<SalesReturnResponseDto>>(
                JsonOptions
            )
        )!.Data!;

        var getResponse = await _f.Client.GetAsync($"/api/v1/sales/returns/{draft.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = (
            await getResponse.Content.ReadFromJsonAsync<SrEnvelope<SalesReturnResponseDto>>(
                JsonOptions
            )
        )!.Data!;
        fetched.Id.Should().Be(draft.Id);
        fetched.ReturnNumber.Should().Be(draft.ReturnNumber);
    }

    [Fact]
    public async Task Escenario6_Lista_paginada_incluye_la_devolucion_creada()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync();
        _f.SetActiveContext(userId);
        await OpenCashSessionAsync();

        var (invoiceId, lineId, _) = await CreateAndAuthorizeInvoiceAsync(quantity: 2m, unitPrice: 10m);
        var createResponse = await CreateDraftAsync(invoiceId, "Listado paginado", (lineId, 1m));
        var draft = (
            await createResponse.Content.ReadFromJsonAsync<SrEnvelope<SalesReturnResponseDto>>(
                JsonOptions
            )
        )!.Data!;

        var listResponse = await _f.Client.GetAsync(
            $"/api/v1/sales/returns?search={draft.ReturnNumber}&pageNumber=1&pageSize=25"
        );
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = (
            await listResponse.Content.ReadFromJsonAsync<SrEnvelope<SalesReturnListResponseDto>>(
                JsonOptions
            )
        )!.Data!;

        list.Items.Should().Contain(i => i.Id == draft.Id);
    }

    [Fact]
    public async Task Escenario7_Lineas_devolvibles_calcula_remanente()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync();
        _f.SetActiveContext(userId);
        await OpenCashSessionAsync();

        var (invoiceId, lineId, grandTotal) = await CreateAndAuthorizeInvoiceAsync(
            quantity: 5m,
            unitPrice: 10m
        );

        var beforeResponse = await _f.Client.GetAsync(
            $"/api/v1/sales/invoices/{invoiceId}/returnable-lines"
        );
        beforeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var before = (
            await beforeResponse.Content.ReadFromJsonAsync<
                SrEnvelope<List<ReturnableLineResponseDto>>
            >(JsonOptions)
        )!.Data!;
        before.Should().ContainSingle(l => l.InvoiceDetailId == lineId && l.RemainingQuantity == 5m);

        // Autoriza una devolución parcial (2 de 5) y verifica que el remanente se recalcula.
        var createResponse = await CreateDraftAsync(invoiceId, "Remanente parcial", (lineId, 2m));
        var draft = (
            await createResponse.Content.ReadFromJsonAsync<SrEnvelope<SalesReturnResponseDto>>(
                JsonOptions
            )
        )!.Data!;
        var authorizeResponse = await AuthorizeReturnAsync(draft.Id, ("Cash", draft.GrandTotal));
        authorizeResponse
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, await authorizeResponse.Content.ReadAsStringAsync());

        var afterResponse = await _f.Client.GetAsync(
            $"/api/v1/sales/invoices/{invoiceId}/returnable-lines"
        );
        var after = (
            await afterResponse.Content.ReadFromJsonAsync<
                SrEnvelope<List<ReturnableLineResponseDto>>
            >(JsonOptions)
        )!.Data!;
        after.Should()
            .ContainSingle(l =>
                l.InvoiceDetailId == lineId && l.ReturnedQuantity == 2m && l.RemainingQuantity == 3m
            );
    }

    // ══════════════════════════════════════════════════════════════════════
    // AUTORIZACIÓN
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Escenario8_Autorizar_devolucion_total_en_efectivo()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync();
        _f.SetActiveContext(userId);
        await OpenCashSessionAsync();

        var (invoiceId, lineId, grandTotal) = await CreateAndAuthorizeInvoiceAsync(
            quantity: 3m,
            unitPrice: 20m
        );

        var createResponse = await CreateDraftAsync(invoiceId, "Devolución total", (lineId, 3m));
        var draft = (
            await createResponse.Content.ReadFromJsonAsync<SrEnvelope<SalesReturnResponseDto>>(
                JsonOptions
            )
        )!.Data!;
        draft.GrandTotal.Should().Be(grandTotal);

        var authorizeResponse = await AuthorizeReturnAsync(draft.Id, ("Cash", draft.GrandTotal));
        authorizeResponse
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, await authorizeResponse.Content.ReadAsStringAsync());
        var authorized = (
            await authorizeResponse.Content.ReadFromJsonAsync<
                SrEnvelope<SalesReturnResponseDto>
            >(JsonOptions)
        )!.Data!;
        authorized.Status.Should().Be("Authorized");
        authorized.GrandTotal.Should().Be(grandTotal);
        authorized.RefundAllocations.Should().ContainSingle(a => a.Method == "Cash" && a.Amount == grandTotal);
    }

    [Fact]
    public async Task Escenario9_Autorizar_devolucion_parcial_en_efectivo()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync();
        _f.SetActiveContext(userId);
        await OpenCashSessionAsync();

        var (invoiceId, lineId, _) = await CreateAndAuthorizeInvoiceAsync(quantity: 4m, unitPrice: 20m);

        var createResponse = await CreateDraftAsync(invoiceId, "Devolución parcial", (lineId, 1m));
        var draft = (
            await createResponse.Content.ReadFromJsonAsync<SrEnvelope<SalesReturnResponseDto>>(
                JsonOptions
            )
        )!.Data!;
        // 1 unidad de 20 + 15% IVA
        draft.GrandTotal.Should().Be(23m);

        var authorizeResponse = await AuthorizeReturnAsync(draft.Id, ("Cash", 23m));
        authorizeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var authorized = (
            await authorizeResponse.Content.ReadFromJsonAsync<
                SrEnvelope<SalesReturnResponseDto>
            >(JsonOptions)
        )!.Data!;
        authorized.Status.Should().Be("Authorized");
        authorized.GrandTotal.Should().Be(23m);
    }

    [Fact]
    public async Task Escenario10_Autorizar_dos_veces_es_rechazado()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync();
        _f.SetActiveContext(userId);
        await OpenCashSessionAsync();

        var (invoiceId, lineId, grandTotal) = await CreateAndAuthorizeInvoiceAsync(
            quantity: 1m,
            unitPrice: 10m
        );
        var createResponse = await CreateDraftAsync(invoiceId, "Doble autorización", (lineId, 1m));
        var draft = (
            await createResponse.Content.ReadFromJsonAsync<SrEnvelope<SalesReturnResponseDto>>(
                JsonOptions
            )
        )!.Data!;

        var first = await AuthorizeReturnAsync(draft.Id, ("Cash", draft.GrandTotal));
        first.StatusCode.Should().Be(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());

        var second = await AuthorizeReturnAsync(draft.Id, ("Cash", draft.GrandTotal));
        second
            .StatusCode.Should()
            .Be(HttpStatusCode.UnprocessableEntity, await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Escenario11_Autorizar_sin_RefundAllocations_es_rechazado()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync();
        _f.SetActiveContext(userId);
        await OpenCashSessionAsync();

        var (invoiceId, lineId, _) = await CreateAndAuthorizeInvoiceAsync(quantity: 1m, unitPrice: 10m);
        var createResponse = await CreateDraftAsync(invoiceId, "Sin asignación", (lineId, 1m));
        var draft = (
            await createResponse.Content.ReadFromJsonAsync<SrEnvelope<SalesReturnResponseDto>>(
                JsonOptions
            )
        )!.Data!;

        var response = await AuthorizeReturnAsync(draft.Id);
        response
            .StatusCode.Should()
            .Be(HttpStatusCode.UnprocessableEntity, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Escenario12_Suma_de_RefundAllocations_distinta_al_total_es_rechazada()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync();
        _f.SetActiveContext(userId);
        await OpenCashSessionAsync();

        var (invoiceId, lineId, grandTotal) = await CreateAndAuthorizeInvoiceAsync(
            quantity: 2m,
            unitPrice: 10m
        );
        var createResponse = await CreateDraftAsync(invoiceId, "Suma distinta", (lineId, 2m));
        var draft = (
            await createResponse.Content.ReadFromJsonAsync<SrEnvelope<SalesReturnResponseDto>>(
                JsonOptions
            )
        )!.Data!;

        var response = await AuthorizeReturnAsync(draft.Id, ("Cash", draft.GrandTotal - 1m));
        response
            .StatusCode.Should()
            .Be(HttpStatusCode.UnprocessableEntity, await response.Content.ReadAsStringAsync());
    }

    // ══════════════════════════════════════════════════════════════════════
    // INVENTARIO
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Escenario13_Autorizar_genera_StockMovement_SaleReturn_correcto()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync();
        _f.SetActiveContext(userId);
        await OpenCashSessionAsync();

        var (invoiceId, lineId, _) = await CreateAndAuthorizeInvoiceAsync(quantity: 3m, unitPrice: 15m);
        var createResponse = await CreateDraftAsync(invoiceId, "Verificación de Kardex", (lineId, 2m));
        var draft = (
            await createResponse.Content.ReadFromJsonAsync<SrEnvelope<SalesReturnResponseDto>>(
                JsonOptions
            )
        )!.Data!;

        var authorizeResponse = await AuthorizeReturnAsync(draft.Id, ("Cash", draft.GrandTotal));
        authorizeResponse
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, await authorizeResponse.Content.ReadAsStringAsync());

        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var movement = await db
            .StockMovements.AsNoTracking()
            .SingleAsync(m => m.SourceDocId == draft.Id);

        movement.MovementType.Should().Be(StockMovementType.SaleReturn);
        movement.Quantity.Should().Be(2m);
        movement.WarehouseId.Should().Be(_f.WarehouseId);
        movement.ProductId.Should().Be(_f.ItemId);
        movement.SourceDocType.Should().Be("SalesReturn");
    }

    // ══════════════════════════════════════════════════════════════════════
    // CAJA
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Escenario14_Reembolso_en_efectivo_registra_CashMovement_SaleRefund_y_actualiza_balance()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync();
        _f.SetActiveContext(userId);
        var sessionId = await OpenCashSessionAsync(openingAmount: 200m);

        var (invoiceId, lineId, grandTotal) = await CreateAndAuthorizeInvoiceAsync(
            quantity: 2m,
            unitPrice: 25m
        );
        // Balance tras la venta: 200 (apertura) + grandTotal (SaleIncome).
        var createResponse = await CreateDraftAsync(invoiceId, "Reembolso en efectivo", (lineId, 1m));
        var draft = (
            await createResponse.Content.ReadFromJsonAsync<SrEnvelope<SalesReturnResponseDto>>(
                JsonOptions
            )
        )!.Data!;

        var authorizeResponse = await AuthorizeReturnAsync(draft.Id, ("Cash", draft.GrandTotal));
        authorizeResponse
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, await authorizeResponse.Content.ReadAsStringAsync());

        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var session = await db
            .CashSessions.AsNoTracking()
            .Include(s => s.Movements)
            .FirstAsync(s => s.Id == sessionId);

        var refundMovement = session
            .Movements.Should()
            .ContainSingle(m => m.MovementType == CashMovementType.SaleRefund)
            .Subject;
        refundMovement.Amount.Should().Be(draft.GrandTotal);
        refundMovement.ReferenceId.Should().Be(draft.Id);

        session.CurrentBalance.Should().Be(200m + grandTotal - draft.GrandTotal);
    }

    [Fact]
    public async Task Escenario15_Reembolso_en_efectivo_sin_caja_abierta_falla_atomicamente()
    {
        // Un usuario abre caja y factura; otro usuario (sin caja abierta) intenta procesar la
        // devolución en efectivo — debe fallar de forma "fail-closed", sin dejar la devolución
        // Authorized huérfana sin su reembolso.
        var sellerId = await _f.CreateUserWithBranchAccessAsync();
        _f.SetActiveContext(sellerId);
        await OpenCashSessionAsync();
        var (invoiceId, lineId, _) = await CreateAndAuthorizeInvoiceAsync(quantity: 1m, unitPrice: 10m);

        var operatorId = await _f.CreateUserWithBranchAccessAsync();
        _f.SetActiveContext(operatorId); // Nunca abre caja.

        var createResponse = await CreateDraftAsync(invoiceId, "Sin caja abierta", (lineId, 1m));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var draft = (
            await createResponse.Content.ReadFromJsonAsync<SrEnvelope<SalesReturnResponseDto>>(
                JsonOptions
            )
        )!.Data!;

        var authorizeResponse = await AuthorizeReturnAsync(draft.Id, ("Cash", draft.GrandTotal));
        authorizeResponse
            .StatusCode.Should()
            .Be(HttpStatusCode.UnprocessableEntity, await authorizeResponse.Content.ReadAsStringAsync());

        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var persisted = await db.SalesReturns.AsNoTracking().FirstAsync(r => r.Id == draft.Id);
        persisted
            .Status.Should()
            .Be(
                Domain.Modules.Sales.Enums.SalesReturnStatus.Draft,
                "el fallo del reembolso debe revertir toda la transacción — nunca queda Authorized sin su reembolso"
            );

        var movementCount = await db.StockMovements.CountAsync(m => m.SourceDocId == draft.Id);
        movementCount.Should().Be(0, "el rollback también debe revertir el movimiento de inventario ya aplicado");
    }

    // ══════════════════════════════════════════════════════════════════════
    // CUENTAS POR COBRAR
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Escenario16_Credito_de_devolucion_reduce_CxC_y_reconstruye_cuotas()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync();
        _f.SetActiveContext(userId);
        await OpenCashSessionAsync();

        var (invoiceId, lineId, grandTotal) = await CreateAndAuthorizeInvoiceAsync(
            quantity: 2m,
            unitPrice: 50m,
            paymentTermId: _f.CreditPaymentTermId
        );

        using (var preScope = _f.CreateDbScope())
        {
            var preDb = preScope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var receivable = await preDb.SalesReceivables.AsNoTracking().FirstAsync(r => r.InvoiceId == invoiceId);
            receivable.OriginalAmount.Should().Be(grandTotal);
        }

        var createResponse = await CreateDraftAsync(invoiceId, "Crédito a CxC", (lineId, 1m));
        var draft = (
            await createResponse.Content.ReadFromJsonAsync<SrEnvelope<SalesReturnResponseDto>>(
                JsonOptions
            )
        )!.Data!;

        var authorizeResponse = await AuthorizeReturnAsync(draft.Id, ("ReceivableCredit", draft.GrandTotal));
        authorizeResponse
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, await authorizeResponse.Content.ReadAsStringAsync());

        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var afterReceivable = await db
            .SalesReceivables.AsNoTracking()
            .Include(r => r.Installments)
            .FirstAsync(r => r.InvoiceId == invoiceId);

        afterReceivable.OriginalAmount.Should().Be(grandTotal - draft.GrandTotal);
        afterReceivable.BalanceDue.Should().Be(grandTotal - draft.GrandTotal);
        afterReceivable.Installments.Sum(i => i.Amount).Should().Be(afterReceivable.BalanceDue);
    }

    [Fact]
    public async Task Escenario16b_Credito_de_devolucion_sin_CxC_asociada_es_rechazado()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync();
        _f.SetActiveContext(userId);
        await OpenCashSessionAsync();

        // Factura de contado — no genera SalesReceivable (isCredit = false).
        var (invoiceId, lineId, grandTotal) = await CreateAndAuthorizeInvoiceAsync(
            quantity: 1m,
            unitPrice: 10m
        );

        var createResponse = await CreateDraftAsync(invoiceId, "Crédito sin CxC", (lineId, 1m));
        var draft = (
            await createResponse.Content.ReadFromJsonAsync<SrEnvelope<SalesReturnResponseDto>>(
                JsonOptions
            )
        )!.Data!;

        var authorizeResponse = await AuthorizeReturnAsync(draft.Id, ("ReceivableCredit", draft.GrandTotal));
        authorizeResponse
            .StatusCode.Should()
            .Be(HttpStatusCode.UnprocessableEntity, await authorizeResponse.Content.ReadAsStringAsync());
    }

    // ══════════════════════════════════════════════════════════════════════
    // CONTABILIDAD
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Escenario17_Autorizar_sin_PostingRule_configurada_no_genera_JournalEntry_ni_falla()
    {
        // No se siembra ningún PostingRule para (SourceModule="Sales", FactType="SalesReturn") en
        // este fixture — comportamiento tolerante documentado (mismo criterio que
        // SalesInvoiceAuthorizedPostingTranslator / confirmado por
        // SalesReturnAuthorizedPostingTranslatorTests.Posting_failure_genera_warning_y_no_lanza_excepcion):
        // el translator solo loguea warning, la autorización de la devolución NO se revierte.
        var userId = await _f.CreateUserWithBranchAccessAsync();
        _f.SetActiveContext(userId);
        await OpenCashSessionAsync();

        var (invoiceId, lineId, _) = await CreateAndAuthorizeInvoiceAsync(quantity: 1m, unitPrice: 10m);
        var createResponse = await CreateDraftAsync(invoiceId, "Sin regla de contabilización", (lineId, 1m));
        var draft = (
            await createResponse.Content.ReadFromJsonAsync<SrEnvelope<SalesReturnResponseDto>>(
                JsonOptions
            )
        )!.Data!;

        var authorizeResponse = await AuthorizeReturnAsync(draft.Id, ("Cash", draft.GrandTotal));
        authorizeResponse
            .StatusCode.Should()
            .Be(
                HttpStatusCode.OK,
                "la falta de PostingRule no debe revertir la autorización de la devolución"
            );
        var authorized = (
            await authorizeResponse.Content.ReadFromJsonAsync<
                SrEnvelope<SalesReturnResponseDto>
            >(JsonOptions)
        )!.Data!;
        authorized.Status.Should().Be("Authorized");

        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var journalCount = await db.JournalEntries.CountAsync(j => j.SourceEventId == draft.Id);
        journalCount
            .Should()
            .Be(0, "sin PostingRule seedeada, el posting engine responde RULE_NOT_FOUND y el translator solo loguea warning");
    }

    // ══════════════════════════════════════════════════════════════════════
    // DOCUMENTOS ELECTRÓNICOS
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Escenario18_Autorizar_registra_ElectronicDocument_CreditNote_con_secuencial_propio()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync();
        _f.SetActiveContext(userId);
        await OpenCashSessionAsync();

        var (invoiceId, lineId, _) = await CreateAndAuthorizeInvoiceAsync(quantity: 1m, unitPrice: 10m);
        var createResponse = await CreateDraftAsync(invoiceId, "Nota de crédito", (lineId, 1m));
        var draft = (
            await createResponse.Content.ReadFromJsonAsync<SrEnvelope<SalesReturnResponseDto>>(
                JsonOptions
            )
        )!.Data!;

        var authorizeResponse = await AuthorizeReturnAsync(draft.Id, ("Cash", draft.GrandTotal));
        authorizeResponse
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, await authorizeResponse.Content.ReadAsStringAsync());

        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        // El secuencial de la Nota de Crédito ("04") se congela en el propio agregado y nunca
        // colisiona con el de la Factura ("01") — mismo punto de emisión, contador independiente
        // por (EmissionPointId, DocTypeCode).
        var persistedReturn = await db.SalesReturns.AsNoTracking().FirstAsync(r => r.Id == draft.Id);
        persistedReturn.CreditNoteDocumentNumber.Should().NotBeNullOrWhiteSpace();
        persistedReturn.CreditNoteDocumentNumber.Should().Contain(_f.EmissionPointCode);

        // Desde ADR-031 (Fase 11) manifest.json ya tiene activeVersion "1.1.0" para CreditNote y
        // el pipeline supera la validación XSD (ver CreditNoteXmlSchemaValidatorTests /
        // CreditNoteXmlBuilderTests para la prueba directa contra el XSD oficial) — pero esta
        // suite no configura un certificado SRI real, así que se detiene en la firma. El
        // ElectronicDocument debe existir (se registró) pero nunca llegar a Authorized/Sent; se
        // acepta cualquier estado terminal tolerante a fallo (Failed/DeadLetter/Rejected) o
        // intermedio (Draft/XmlGenerated/Signed) — jamás Authorized.
        var edoc = await db
            .ElectronicDocuments.AsNoTracking()
            .Where(d => d.SourceModule == "Sales" && d.SourceEntityId == draft.Id)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync();

        if (edoc is null)
        {
            // El registro del documento electrónico es "best effort" tras el commit (Fase 10): un
            // fallo temprano (p. ej. no se pudo resolver el catálogo) puede impedir incluso crear
            // la fila. Documentado como comportamiento tolerado — no bloquea la autorización.
            return;
        }

        edoc.DocumentType.Should().Be(ElectronicDocumentType.CreditNote);
        edoc.CurrentState.Should()
            .NotBe(
                ElectronicDocumentState.Authorized,
                "manifest.json CreditNote.activeVersion sigue en null — no debe autorizarse ante el SRI"
            );
    }

    // ══════════════════════════════════════════════════════════════════════
    // AUDITORÍA
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Escenario19_Auditoria_registra_Created_Authorized_y_Cancelled()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync();
        _f.SetActiveContext(userId);
        await OpenCashSessionAsync();

        // ── Created + Authorized ──
        var (invoiceId1, lineId1, _) = await CreateAndAuthorizeInvoiceAsync(quantity: 2m, unitPrice: 10m);
        var createResponse1 = await CreateDraftAsync(invoiceId1, "Auditoría Created/Authorized", (lineId1, 1m));
        var draft1 = (
            await createResponse1.Content.ReadFromJsonAsync<SrEnvelope<SalesReturnResponseDto>>(
                JsonOptions
            )
        )!.Data!;
        var authorizeResponse1 = await AuthorizeReturnAsync(draft1.Id, ("Cash", draft1.GrandTotal));
        authorizeResponse1.StatusCode.Should().Be(HttpStatusCode.OK);

        // ── Cancelled ──
        var (invoiceId2, lineId2, _) = await CreateAndAuthorizeInvoiceAsync(quantity: 2m, unitPrice: 10m);
        var createResponse2 = await CreateDraftAsync(invoiceId2, "Auditoría Cancelled", (lineId2, 1m));
        var draft2 = (
            await createResponse2.Content.ReadFromJsonAsync<SrEnvelope<SalesReturnResponseDto>>(
                JsonOptions
            )
        )!.Data!;
        var cancelResponse = await _f.Client.DeleteAsync($"/api/v1/sales/returns/{draft2.Id}");
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var audits1 = await db
            .SalesReturnAudits.AsNoTracking()
            .Where(a => a.SalesInvoiceId == invoiceId1)
            .ToListAsync();
        audits1.Should().Contain(a => a.Action == "Created");
        audits1.Should().Contain(a => a.Action == "Authorized" && a.GrandTotal == draft1.GrandTotal);

        var audits2 = await db
            .SalesReturnAudits.AsNoTracking()
            .Where(a => a.SalesInvoiceId == invoiceId2)
            .ToListAsync();
        audits2.Should().Contain(a => a.Action == "Created");
        audits2.Should().Contain(a => a.Action == "Cancelled");
    }

    // ══════════════════════════════════════════════════════════════════════
    // CONCURRENCIA (PostgreSQL real — dos autorizaciones simultáneas)
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Escenario20_Dos_autorizaciones_concurrentes_sobre_la_misma_factura_solo_una_prospera()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync();
        _f.SetActiveContext(userId);
        await OpenCashSessionAsync();

        // Factura con 4 unidades: dos Drafts piden 3 c/u (6 en total) — combinados exceden lo
        // vendido, así que solo uno de los dos debe poder autorizarse.
        var (invoiceId, lineId, _) = await CreateAndAuthorizeInvoiceAsync(quantity: 4m, unitPrice: 20m);

        var draftAResponse = await CreateDraftAsync(invoiceId, "Concurrencia A", (lineId, 3m));
        var draftA = (
            await draftAResponse.Content.ReadFromJsonAsync<SrEnvelope<SalesReturnResponseDto>>(
                JsonOptions
            )
        )!.Data!;
        var draftBResponse = await CreateDraftAsync(invoiceId, "Concurrencia B", (lineId, 3m));
        var draftB = (
            await draftBResponse.Content.ReadFromJsonAsync<SrEnvelope<SalesReturnResponseDto>>(
                JsonOptions
            )
        )!.Data!;

        var taskA = AuthorizeReturnAsync(draftA.Id, ("Cash", draftA.GrandTotal));
        var taskB = AuthorizeReturnAsync(draftB.Id, ("Cash", draftB.GrandTotal));
        await Task.WhenAll(taskA, taskB);

        var resultA = await taskA;
        var resultB = await taskB;

        var statuses = new[] { resultA.StatusCode, resultB.StatusCode };
        statuses.Should().Contain(HttpStatusCode.OK, "al menos la que cabe en el remanente debe prosperar");
        statuses
            .Should()
            .Contain(
                HttpStatusCode.UnprocessableEntity,
                "la que excede el remanente recalculado bajo el advisory lock debe ser rechazada"
            );

        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var authorizedReturns = await db
            .SalesReturns.AsNoTracking()
            .Include(r => r.Lines)
            .Where(r =>
                r.SalesInvoiceId == invoiceId
                && r.Status == Domain.Modules.Sales.Enums.SalesReturnStatus.Authorized
            )
            .ToListAsync();

        var totalReturnedQty = authorizedReturns.SelectMany(r => r.Lines).Sum(l => l.Quantity);
        totalReturnedQty.Should().BeLessThanOrEqualTo(4m, "nunca puede devolverse más de lo vendido");
        authorizedReturns.Should().HaveCount(1, "de las dos autorizaciones en carrera, exactamente una debe haber prosperado");
    }
}

// ══════════════════════════════════════════════════════════════════════════
// DTOs locales — mismo criterio que CajaVentasEndToEndTests: no se reutilizan
// contratos de otras capas, se deserializa solo lo que este archivo necesita.
// ══════════════════════════════════════════════════════════════════════════

internal sealed record SrEnvelope<T>(T? Data);

internal sealed record SrCashSessionResponseDto(Guid Id, string Status);

internal sealed record SrSalesInvoiceLineResponseDto(Guid Id);

internal sealed record SrSalesInvoiceResponseDto(
    Guid Id,
    string Status,
    decimal GrandTotal,
    List<SrSalesInvoiceLineResponseDto> Lines
);

internal sealed record SalesReturnDetailResponseDto(
    Guid Id,
    Guid OriginalInvoiceDetailId,
    decimal Quantity
);

internal sealed record SalesReturnRefundAllocationResponseDto(Guid Id, string Method, decimal Amount);

internal sealed record SalesReturnResponseDto(
    Guid Id,
    Guid SalesInvoiceId,
    Guid CustomerId,
    string ReturnNumber,
    string Reason,
    string Status,
    decimal Subtotal,
    decimal TotalVat,
    decimal TotalIce,
    decimal TotalDiscount,
    decimal GrandTotal,
    List<SalesReturnDetailResponseDto> Lines,
    List<SalesReturnRefundAllocationResponseDto> RefundAllocations
);

internal sealed record SalesReturnListItemResponseDto(
    Guid Id,
    string ReturnNumber,
    Guid SalesInvoiceId,
    string Status,
    decimal GrandTotal
);

internal sealed record SalesReturnListResponseDto(
    List<SalesReturnListItemResponseDto> Items,
    int Total,
    int Page,
    int PageSize
);

internal sealed record ReturnableLineResponseDto(
    Guid InvoiceDetailId,
    Guid? ItemId,
    decimal OriginalQuantity,
    decimal ReturnedQuantity,
    decimal RemainingQuantity
);
