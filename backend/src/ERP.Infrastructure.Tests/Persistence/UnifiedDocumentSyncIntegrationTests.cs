using ERP.Application.Common;
using ERP.Application.Common.Config;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// Prueba el puente legacy → sales_document con PostgreSQL local (docker compose).
/// </summary>
public sealed class UnifiedDocumentSyncIntegrationTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5435;Database=dberpsaas;Username=postgres;Password=zhin@2024";

    internal static readonly Guid SubscriberId = Guid.Parse("f69a1f0e-3b1d-4ecd-9bb1-5b3e290bfb83");
    internal static readonly Guid CustomerId = Guid.Parse("d4aea039-787d-4433-9081-16e9108ef11d");
    internal static readonly Guid BranchId = Guid.Parse("e24d1d82-46b9-40bb-a11f-89ac524f9597");
    internal static readonly Guid WarehouseId = Guid.Parse("c2b21669-67f5-4b60-83a2-b27a94f21f2d");
    internal static readonly Guid UserId = Guid.Parse("71ac8b3b-13db-47db-8cf7-214721a3a31f");
    internal static readonly Guid CompanyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static ICurrentCompany CreateTestCompany()
    {
        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        company.Setup(c => c.HasCompanyContext).Returns(true);
        company.Setup(c => c.IsAuthenticated).Returns(true);
        return company.Object;
    }

    private static async Task<bool> CanConnectAsync()
    {
        try
        {
            await using var ctx = CreateContext();
            return await ctx.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    private static ErpDbContext CreateContext()
    {
        var tenant = new Mock<ICurrentSubscriber>();
        tenant.Setup(t => t.SubscriberId).Returns(SubscriberId);
        return TestErpDbContextFactory.Create(
            new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(ConnectionString).Options,
            tenant.Object,
            Mock.Of<IPublisher>(),
            CreateTestCompany());
    }

    private static ServiceProvider BuildServices()
    {
        var tenant = new Mock<ICurrentSubscriber>();
        tenant.Setup(t => t.SubscriberId).Returns(SubscriberId);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ICurrentSubscriber>(tenant.Object);
        services.AddSingleton<ICurrentCompany>(CreateTestCompany());
        services.AddSingleton(Mock.Of<IPublisher>());
        services.Configure<SubscriberEntitlementsOptions>(_ => { });
        services.AddScoped<IPlatformQueryAccessor, PlatformQueryAccessor>();
        services.AddDbContext<ErpDbContext>((_, o) =>
            o.UseNpgsql(ConnectionString, b => b.MigrationsAssembly(typeof(ErpDbContext).Assembly.FullName)));
        services.AddScoped<IUnifiedDocumentSync, UnifiedDocumentSync>();
        services.AddScoped<ISalesRepository, SalesRepository>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task CreateBill_WithUnifiedSchema_WritesToSalesDocument()
    {
        if (!await CanConnectAsync())
            return;

        await using var sp = BuildServices();
        var repo = sp.GetRequiredService<ISalesRepository>();
        var ctx = sp.GetRequiredService<ErpDbContext>();
        if (!await UnifiedIntegrationTestSeed.TryEnsureAsync(ctx))
            return;

        var suffix = Guid.NewGuid().ToString("N")[..9];
        var bill = BuildBill(suffix);
        var line = SalesBillLine.Create(
            SubscriberId, Guid.NewGuid(), "TEST-001", 1m, 100m, 0m,
            "4", 15m, 15m, "Línea prueba sync", UserId);
        bill.AddLine(line);

        Guid billId = bill.Id;
        try
        {
            await repo.AddBillAsync(bill);
            await repo.SaveChangesAsync();

            var inUnified = await ctx.SalesDocuments
                .AsNoTracking()
                .Include(d => d.Lines)
                .FirstOrDefaultAsync(d => d.Id == billId);

            inUnified.Should().NotBeNull();
            inUnified!.Status.Should().Be("Borrador");
            inUnified.Lines.Should().HaveCount(1);
            inUnified.Total.Should().Be(115m);

            var inLegacy = await ctx.SalesBills.AsNoTracking().AnyAsync(b => b.Id == billId);
            inLegacy.Should().BeFalse();
        }
        finally
        {
            await CleanupBillAsync(ctx, billId);
        }
    }

    [Fact]
    public async Task ValidateBill_AfterLoad_PersistsStatusToSalesDocument()
    {
        if (!await CanConnectAsync())
            return;

        await using var sp = BuildServices();
        var repo = sp.GetRequiredService<ISalesRepository>();
        var ctx = sp.GetRequiredService<ErpDbContext>();
        if (!await UnifiedIntegrationTestSeed.TryEnsureAsync(ctx))
            return;

        var suffix = Guid.NewGuid().ToString("N")[..9];
        var bill = BuildBill(suffix);
        bill.AddLine(SalesBillLine.Create(
            SubscriberId, Guid.NewGuid(), "TEST-002", 2m, 50m, 0m,
            "0", 0m, 0m, "Línea sin IVA", UserId));

        Guid billId = bill.Id;
        try
        {
            await repo.AddBillAsync(bill);
            await repo.SaveChangesAsync();

            var loaded = await repo.GetBillByIdAsync(SubscriberId, billId);
            loaded.Should().NotBeNull();
            loaded!.Validate(UserId);
            await repo.SaveChangesAsync();

            var doc = await ctx.SalesDocuments.AsNoTracking()
                .FirstAsync(d => d.Id == billId);
            doc.Status.Should().Be("Validado");
            doc.UpdatedBy.Should().Be(UserId);
        }
        finally
        {
            await CleanupBillAsync(ctx, billId);
        }
    }

    [Fact]
    public async Task AuthorizeBill_PersistsElectronicDoc()
    {
        if (!await CanConnectAsync())
            return;

        await using var sp = BuildServices();
        var repo = sp.GetRequiredService<ISalesRepository>();
        var ctx = sp.GetRequiredService<ErpDbContext>();
        if (!await UnifiedIntegrationTestSeed.TryEnsureAsync(ctx))
            return;

        var suffix = Guid.NewGuid().ToString("N")[..9];
        var bill = BuildBill(suffix);
        bill.AddLine(SalesBillLine.Create(
            SubscriberId, Guid.NewGuid(), "TEST-003", 1m, 10m, 0m,
            "4", 15m, 1.5m, "Mini", UserId));

        Guid billId = bill.Id;
        try
        {
            await repo.AddBillAsync(bill);
            await repo.SaveChangesAsync();

            var loaded = await repo.GetBillByIdAsync(SubscriberId, billId);
            loaded!.Validate(UserId);
            loaded.Authorize(
                UserId,
                "1234567890",
                DateTime.UtcNow,
                "/xml/signed/test.xml",
                "/xml/auth/test.xml",
                Guid.NewGuid());
            await repo.SaveChangesAsync();

            var electronic = await ctx.SalesElectronicDocs.AsNoTracking()
                .FirstOrDefaultAsync(e => e.SalesDocumentId == billId);

            electronic.Should().NotBeNull();
            electronic!.AuthNumber.Should().Be("1234567890");
            electronic.XmlSignedPath.Should().Be("/xml/signed/test.xml");
            electronic.XmlAuthPath.Should().Be("/xml/auth/test.xml");

            var doc = await ctx.SalesDocuments.AsNoTracking()
                .FirstAsync(d => d.Id == billId);
            doc.Status.Should().Be("Autorizado");
        }
        finally
        {
            await CleanupBillAsync(ctx, billId);
        }
    }

    [Fact]
    public async Task AddRetention_WithUnifiedSchema_WritesToSalesWithholding()
    {
        if (!await CanConnectAsync())
            return;

        await using var sp = BuildServices();
        var repo = sp.GetRequiredService<ISalesRepository>();
        var ctx = sp.GetRequiredService<ErpDbContext>();
        if (!await UnifiedIntegrationTestSeed.TryEnsureAsync(ctx))
            return;

        var suffix = Guid.NewGuid().ToString("N")[..9];
        var bill = BuildBill(suffix);
        bill.AddLine(SalesBillLine.Create(
            SubscriberId, Guid.NewGuid(), "RET-001", 1m, 100m, 0m,
            "4", 15m, 15m, "Base retención", UserId));

        Guid billId = bill.Id;
        Guid retentionId = Guid.Empty;
        try
        {
            await repo.AddBillAsync(bill);
            await repo.SaveChangesAsync();
            bill = (await repo.GetBillByIdAsync(SubscriberId, billId))!;
            bill.Validate(UserId);
            bill.Authorize(UserId, "999", DateTime.UtcNow, null, null, Guid.NewGuid());
            await repo.SaveChangesAsync();

            var clave = new string('8', 49);
            var retention = SalesRetention.Create(
                SubscriberId, CustomerId, clave, DateTime.UtcNow.Date, 15m, billId, "/xml/ret.xml", UserId);
            var line = SalesRetentionLine.Create(
                SubscriberId, "IVA", "303", 100m, 15m, 15m, UserId);
            line.AssignRetentionId(retention.Id);
            retention.AddLine(line);
            retentionId = retention.Id;

            await repo.AddRetentionAsync(retention);
            await repo.SaveChangesAsync();

            var inUnified = await ctx.SalesWithholdings.AsNoTracking()
                .Include(w => w.Lines)
                .FirstOrDefaultAsync(w => w.Id == retentionId);

            inUnified.Should().NotBeNull();
            inUnified!.Direction.Should().Be(ERP.Domain.Common.WithholdingDirection.Received);
            inUnified.SalesDocumentId.Should().Be(billId);
            inUnified.Lines.Should().HaveCount(1);

            var inLegacy = await ctx.SalesRetentions.AsNoTracking().AnyAsync(r => r.Id == retentionId);
            inLegacy.Should().BeFalse();
        }
        finally
        {
            await CleanupRetentionAsync(ctx, retentionId);
            await CleanupBillAsync(ctx, billId);
        }
    }

    private static async Task CleanupRetentionAsync(ErpDbContext ctx, Guid retentionId)
    {
        if (retentionId == Guid.Empty) return;
        var lines = await ctx.SalesWithholdingLines.Where(l => l.SalesWithholdingId == retentionId).ToListAsync();
        ctx.SalesWithholdingLines.RemoveRange(lines);
        var w = await ctx.SalesWithholdings.FirstOrDefaultAsync(x => x.Id == retentionId);
        if (w is not null) ctx.SalesWithholdings.Remove(w);
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task RejectBill_PersistsErrorOnElectronicDoc()
    {
        if (!await CanConnectAsync())
            return;

        await using var sp = BuildServices();
        var repo = sp.GetRequiredService<ISalesRepository>();
        var ctx = sp.GetRequiredService<ErpDbContext>();
        if (!await UnifiedIntegrationTestSeed.TryEnsureAsync(ctx))
            return;

        var suffix = Guid.NewGuid().ToString("N")[..9];
        var bill = BuildBill(suffix);
        bill.AddLine(SalesBillLine.Create(
            SubscriberId, Guid.NewGuid(), "TEST-004", 1m, 10m, 0m,
            "0", 0m, 0m, "Rechazo", UserId));

        Guid billId = bill.Id;
        try
        {
            await repo.AddBillAsync(bill);
            await repo.SaveChangesAsync();

            var loaded = await repo.GetBillByIdAsync(SubscriberId, billId);
            loaded!.Validate(UserId);
            loaded.Reject(UserId, "Error SRI simulado");
            await repo.SaveChangesAsync();

            var electronic = await ctx.SalesElectronicDocs.AsNoTracking()
                .FirstOrDefaultAsync(e => e.SalesDocumentId == billId);

            electronic.Should().NotBeNull();
            electronic!.ErrorMessage.Should().Be("Error SRI simulado");

            var doc = await ctx.SalesDocuments.AsNoTracking()
                .FirstAsync(d => d.Id == billId);
            doc.Status.Should().Be("Rechazado");
        }
        finally
        {
            await CleanupBillAsync(ctx, billId);
        }
    }

    private static SalesBill BuildBill(string sequentialSuffix)
    {
        var accessKey = new string('9', 49);
        return SalesBill.Create(
            SubscriberId,
            BranchId,
            CustomerId,
            WarehouseId,
            "01",
            "001",
            "001",
            sequentialSuffix,
            accessKey,
            DateTime.UtcNow.Date,
            100m,
            15m,
            115m,
            0m,
            "01",
            0,
            "Prueba unified sync",
            null,
            null,
            null,
            null,
            null,
            UserId);
    }

    private static async Task CleanupBillAsync(ErpDbContext ctx, Guid billId)
    {
        var electronic = await ctx.SalesElectronicDocs
            .FirstOrDefaultAsync(e => e.SalesDocumentId == billId);
        if (electronic is not null)
            ctx.SalesElectronicDocs.Remove(electronic);

        var lines = await ctx.SalesDetails.Where(l => l.SalesDocumentId == billId).ToListAsync();
        ctx.SalesDetails.RemoveRange(lines);

        var doc = await ctx.SalesDocuments.FirstOrDefaultAsync(d => d.Id == billId);
        if (doc is not null)
            ctx.SalesDocuments.Remove(doc);

        await ctx.SaveChangesAsync();
    }
}
