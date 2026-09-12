using ERP.Application.Common;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Modules.SriCatalogs.Interfaces;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Seeding;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Infrastructure.Tests.Seeding;

/// <summary>
/// SALES-PAYMENT-METHOD-SRI-MAPPING-EFECTIVO-WRONG-CODE-01 — causa raíz confirmada en BD real
/// (dev): tenants creados antes de SALES-PAYMENT-METHOD-SRI-MAPPING-SSOT-01 tienen sus 5
/// PaymentMethod Tipo A con SriPaymentMethodCode NULL (SalesBootstrapStep solo siembra empresas
/// NUEVAS), así que el frontend cae al default de empresa (p. ej. "20") incluso para Efectivo.
/// Estas pruebas cubren PaymentMethodSriMappingBackfillService, la corrección.
/// </summary>
public sealed class PaymentMethodSriMappingBackfillServiceTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();

    private static ErpDbContext NewDbContext(string dbName) =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>()
                .UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options,
            new FixedCurrentTenant(TenantA),
            new NoOpPublisher(),
            new FixedCurrentCompany(Guid.NewGuid())
        );

    private static PaymentMethodSriMappingBackfillService NewService(
        ErpDbContext db,
        IEnumerable<string> activeCatalogCodes
    )
    {
        var catalogRepo = new Mock<ISriCatalogLookupRepository>();
        catalogRepo
            .Setup(r => r.GetActivePaymentMethodsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                activeCatalogCodes
                    .Select(code => new SriPaymentMethod { Code = code, Name = $"Nombre {code}" })
                    .ToList()
            );
        return new PaymentMethodSriMappingBackfillService(
            db,
            catalogRepo.Object,
            NullLogger<PaymentMethodSriMappingBackfillService>.Instance
        );
    }

    // Catálogo SRI real usado en las pruebas: 01, 15, 19, 20 activos.
    private static readonly string[] FullCatalog = ["01", "15", "16", "19", "20"];

    [Fact]
    public async Task RunAsync_EFECTIVO_sin_mapeo_recibe_backfill_a_01()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = NewDbContext(dbName);
        var cash = PaymentMethod.Create(TenantA, "EFECTIVO", "Efectivo", false, false, 1, ActorId);
        db.PaymentMethods.Add(cash);
        await db.SaveChangesAsync();

        var service = NewService(db, FullCatalog);
        var result = await service.RunAsync();

        result.RowsUpdated.Should().Be(1);
        var reloaded = await db.PaymentMethods.IgnoreQueryFilters().SingleAsync(pm => pm.Id == cash.Id);
        reloaded.SriPaymentMethodCode.Should().Be("01");
    }

    [Fact]
    public async Task RunAsync_TRANSFERENCIA_y_CHEQUE_reciben_20()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = NewDbContext(dbName);
        var transfer = PaymentMethod.Create(
            TenantA, "TRANSFERENCIA", "Transferencia Bancaria", true, false, 3, ActorId,
            PaymentMethodDetailType.Transfer
        );
        var cheque = PaymentMethod.Create(
            TenantA, "CHEQUE", "Cheque", true, false, 4, ActorId, PaymentMethodDetailType.Check
        );
        db.PaymentMethods.AddRange(transfer, cheque);
        await db.SaveChangesAsync();

        var service = NewService(db, FullCatalog);
        var result = await service.RunAsync();

        result.RowsUpdated.Should().Be(2);
        (await db.PaymentMethods.IgnoreQueryFilters().SingleAsync(pm => pm.Id == transfer.Id))
            .SriPaymentMethodCode.Should().Be("20");
        (await db.PaymentMethods.IgnoreQueryFilters().SingleAsync(pm => pm.Id == cheque.Id))
            .SriPaymentMethodCode.Should().Be("20");
    }

    [Fact]
    public async Task RunAsync_TARJETA_CREDITO_recibe_19()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = NewDbContext(dbName);
        var card = PaymentMethod.Create(
            TenantA, "TARJETA", "Tarjeta de Crédito", true, false, 2, ActorId,
            PaymentMethodDetailType.Card
        );
        db.PaymentMethods.Add(card);
        await db.SaveChangesAsync();

        var service = NewService(db, FullCatalog);
        await service.RunAsync();

        (await db.PaymentMethods.IgnoreQueryFilters().SingleAsync(pm => pm.Id == card.Id))
            .SriPaymentMethodCode.Should().Be("19");
    }

    [Fact]
    public async Task RunAsync_no_pisa_valores_ya_configurados_manualmente()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = NewDbContext(dbName);
        var cash = PaymentMethod.Create(TenantA, "EFECTIVO", "Efectivo", false, false, 1, ActorId);
        // Simula que el operador ya configuró manualmente un código distinto al sugerido (p. ej.
        // "15" — Compensación de deudas) antes de correr el backfill.
        cash.Update(cash.Name, cash.RequiresReference, cash.IsCreditAllowed, cash.SortOrder, ActorId, cash.DetailType, "15");
        db.PaymentMethods.Add(cash);
        await db.SaveChangesAsync();

        var service = NewService(db, FullCatalog);
        var result = await service.RunAsync();

        result.RowsUpdated.Should().Be(0, because: "ya tenía un mapeo configurado, el backfill no debe tocarlo");
        (await db.PaymentMethods.IgnoreQueryFilters().SingleAsync(pm => pm.Id == cash.Id))
            .SriPaymentMethodCode.Should().Be("15");
    }

    [Fact]
    public async Task RunAsync_no_escribe_codigo_sugerido_si_no_existe_activo_en_catalogo_sri()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = NewDbContext(dbName);
        var cash = PaymentMethod.Create(TenantA, "EFECTIVO", "Efectivo", false, false, 1, ActorId);
        db.PaymentMethods.Add(cash);
        await db.SaveChangesAsync();

        // Catálogo SRI sin el código "01" activo (escenario límite) — el backfill nunca debe
        // escribir un código a ciegas, aunque sea el sugerido por DefaultPaymentMethodSeedData.
        var service = NewService(db, ["15", "19", "20"]);
        var result = await service.RunAsync();

        result.RowsUpdated.Should().Be(0);
        result.SkippedInactiveCatalog.Should().Be(1);
        (await db.PaymentMethods.IgnoreQueryFilters().SingleAsync(pm => pm.Id == cash.Id))
            .SriPaymentMethodCode.Should().BeNull();
    }

    [Fact]
    public async Task RunAsync_CREDITO_sin_mapeo_sugerido_se_omite_sin_error()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = NewDbContext(dbName);
        var credito = PaymentMethod.Create(
            TenantA, "CREDITO", "Crédito", false, true, 5, ActorId
        );
        db.PaymentMethods.Add(credito);
        await db.SaveChangesAsync();

        var service = NewService(db, FullCatalog);
        var result = await service.RunAsync();

        result.RowsUpdated.Should().Be(0);
        result.SkippedNoMapping.Should().Be(1);
        (await db.PaymentMethods.IgnoreQueryFilters().SingleAsync(pm => pm.Id == credito.Id))
            .SriPaymentMethodCode.Should().BeNull();
    }

    [Fact]
    public async Task RunAsync_es_multi_tenant_seguro_no_cruza_TenantId_entre_filas()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = NewDbContext(dbName);
        var cashTenantA = PaymentMethod.Create(TenantA, "EFECTIVO", "Efectivo", false, false, 1, ActorId);
        var cashTenantB = PaymentMethod.Create(TenantB, "EFECTIVO", "Efectivo", false, false, 1, ActorId);
        db.PaymentMethods.AddRange(cashTenantA, cashTenantB);
        await db.SaveChangesAsync();

        var service = NewService(db, FullCatalog);
        var result = await service.RunAsync();

        result.RowsUpdated.Should().Be(2);
        var reloadedA = await db.PaymentMethods.IgnoreQueryFilters().SingleAsync(pm => pm.Id == cashTenantA.Id);
        var reloadedB = await db.PaymentMethods.IgnoreQueryFilters().SingleAsync(pm => pm.Id == cashTenantB.Id);
        reloadedA.TenantId.Should().Be(TenantA);
        reloadedB.TenantId.Should().Be(TenantB);
        reloadedA.SriPaymentMethodCode.Should().Be("01");
        reloadedB.SriPaymentMethodCode.Should().Be("01");
    }

    [Fact]
    public async Task RunAsync_es_idempotente_segunda_corrida_no_cambia_nada()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = NewDbContext(dbName);
        var cash = PaymentMethod.Create(TenantA, "EFECTIVO", "Efectivo", false, false, 1, ActorId);
        db.PaymentMethods.Add(cash);
        await db.SaveChangesAsync();

        var service = NewService(db, FullCatalog);
        var first = await service.RunAsync();
        var second = await service.RunAsync();

        first.RowsUpdated.Should().Be(1);
        second.RowsUpdated.Should().Be(0, because: "ya no quedan filas con SriPaymentMethodCode null");
    }

    private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Guid companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => companyId != Guid.Empty;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }
}
