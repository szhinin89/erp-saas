using System.Text;
using ERP.Application.Common;
using ERP.Application.Common.Models;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.ImportPurchaseReception;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Modules.Purchases.PurchaseReception;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Interceptors;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Modules.Purchases.PurchaseReception;

/// <summary>
/// PURCHASES-RECEPTION-CROSS-COMPANY-KEY-01 (H2) contra PostgreSQL real: la clave de acceso es única por
/// tenant (<c>uq_purchase_reception_documents_tenant_access_key</c>) y las búsquedas se acotan a la
/// empresa. Una clave ya registrada en otra empresa, o insertada por una importación simultánea, se
/// devuelve como conflicto genérico, revierte el archivo completo y no expone datos de la otra empresa.
/// Parser real (TXT sintético), repositorios reales y traductor PostgreSQL real; sin SRI.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class ImportPurchaseReceptionCrossCompanyPostgreSqlTests : IAsyncLifetime
{
    private const string RucA = "1790000000001";
    private const string RucB = "0990000000001";
    private const string LegalNameA = "Empresa Receptora A S.A.";

    private static readonly string KeyShared = new('1', 49);
    private static readonly string KeyNew = new('2', 49);
    private static readonly string KeyRace = new('3', 49);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_reception_h2_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly Guid _userId = Guid.NewGuid();
    private Guid _tenantId;
    private Guid _companyA;
    private Guid _companyB;
    private Guid _branchA;
    private Guid _branchB;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = CreateContext(Guid.Empty, Guid.Empty);
        await db.Database.MigrateAsync();

        var tenant = Tenant.Create("Tenant H2", $"h2-{Guid.NewGuid():N}"[..16], _userId);
        var companyA = Company.CreateManaged(tenant.Id, RucA, LegalNameA, createdBy: _userId);
        var companyB = Company.CreateManaged(tenant.Id, RucB, "Empresa Receptora B S.A.", createdBy: _userId);
        var branchA = NewBranch(tenant.Id, companyA.Id, "001");
        var branchB = NewBranch(tenant.Id, companyB.Id, "002");
        db.Tenants.Add(tenant);
        db.Companies.AddRange(companyA, companyB);
        db.Branches.AddRange(branchA, branchB);
        await db.SaveChangesAsync();

        (_tenantId, _companyA, _companyB, _branchA, _branchB) =
            (tenant.Id, companyA.Id, companyB.Id, branchA.Id, branchB.Id);
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private Branch NewBranch(Guid tenantId, Guid companyId, string code) =>
        Branch.Create(
            tenantId, "Matriz", "Av. Principal 123", code,
            null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, true, _userId, companyId: companyId);

    private ErpDbContext CreateContext(Guid tenantId, Guid companyId) =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>()
                .UseNpgsql(_postgres.GetConnectionString())
                .AddInterceptors(new NewChildEntityTrackingInterceptor())
                .Options,
            new FixedCurrentTenant(tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(companyId)
        );

    private ImportPurchaseReceptionHandler Handler(
        ErpDbContext db,
        Guid companyId,
        Guid branchId,
        IPurchaseCreditNoteRepository? creditNotes = null
    )
    {
        var verifier = new Mock<IPurchaseReceptionVerifier>();
        verifier
            .Setup(v => v.VerifyAsync(It.IsAny<IReadOnlyList<PurchaseReceptionRecord>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<PurchaseReceptionRecord> rows, CancellationToken _) =>
                rows.Select(r => new PurchaseReceptionVerifiedItem(r, false, PurchaseReceptionStatus.NewSupplier))
                    .ToList());
        var clock = new Mock<ICompanyClock>();
        clock
            .Setup(c => c.CompanyLocalToUtcAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid _, DateTime local, CancellationToken _) =>
                DateTime.SpecifyKind(local.AddHours(5), DateTimeKind.Utc));

        return new ImportPurchaseReceptionHandler(
            new PurchaseInvoiceTxtParser(),
            verifier.Object,
            new PurchaseReceptionDocumentRepository(db, new FixedCurrentCompany(companyId)),
            creditNotes ?? Mock.Of<IPurchaseCreditNoteRepository>(),
            new FixedCurrentTenant(_tenantId),
            new FixedCurrentCompany(companyId),
            Mock.Of<ICurrentBranch>(b => b.BranchId == branchId),
            Mock.Of<ICurrentUser>(u => u.UserId == _userId),
            clock.Object,
            new CompanyRepository(db),
            new PostgresDatabaseExceptionTranslator()
        );
    }

    private static ImportPurchaseReceptionCommand Txt(string receiver, params (string Type, string Key)[] rows)
    {
        var sb = new StringBuilder(
            "RUC_EMISOR\tRAZON_SOCIAL_EMISOR\tTIPO_COMPROBANTE\tSERIE_COMPROBANTE\tCLAVE_ACCESO\t"
                + "FECHA_AUTORIZACION\tFECHA_EMISION\tIDENTIFICACION_RECEPTOR\tVALOR_SIN_IMPUESTOS\tIVA\t"
                + "IMPORTE_TOTAL\tNUMERO_DOCUMENTO_MODIFICADO\n");
        var n = 0;
        foreach (var (type, key) in rows)
        {
            n++;
            var modified = type == "Factura" ? "" : "001-001-000000001";
            sb.Append($"1791111111001\tPROVEEDOR S.A.\t{type}\t001-001-00000000{n}\t{key}\t")
                .Append($"01/07/2026 10:00:00\t01/07/2026\t{receiver}\t10.00\t1.50\t11.50\t{modified}\n");
        }
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return new ImportPurchaseReceptionCommand(
            new MediaUploadContent(new MemoryStream(bytes), "recibidos.txt", "text/plain", bytes.Length));
    }

    private async Task<List<PurchaseReceptionDocument>> AllTenantDocumentsAsync()
    {
        await using var db = CreateContext(_tenantId, Guid.Empty);
        return await db.PurchaseReceptionDocuments.IgnoreQueryFilters().AsNoTracking()
            .Where(d => d.TenantId == _tenantId)
            .ToListAsync();
    }

    [Fact]
    public async Task Key_registered_by_another_company_rejects_the_whole_file_with_a_generic_conflict()
    {
        await using (var dbA = CreateContext(_tenantId, _companyA))
        {
            var imported = await Handler(dbA, _companyA, _branchA).Handle(
                Txt(RucA, ("Factura", KeyShared)), CancellationToken.None);
            imported.IsSuccess.Should().BeTrue(imported.Error);
        }

        Result<ERP.Application.Modules.Purchases.PurchaseReception.DTOs.PurchaseReceptionImportResultDto> result;
        await using (var dbB = CreateContext(_tenantId, _companyB))
        {
            result = await Handler(dbB, _companyB, _branchB).Handle(
                Txt(RucB, ("Factura", KeyNew), ("Factura", KeyShared)), CancellationToken.None);
        }

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
        result.Error.Should().Be(ImportPurchaseReceptionHandler.AccessKeyConflictMessage);
        result.Error.Should().NotContain(RucA).And.NotContain(LegalNameA).And.NotContain(KeyShared);

        var documents = await AllTenantDocumentsAsync();
        documents.Should().ContainSingle("la fila nueva de B se revierte con el archivo completo");
        documents[0].AccessKey.Should().Be(KeyShared);
        documents[0].CompanyId.Should().Be(_companyA);
    }

    [Fact]
    public async Task Concurrent_insert_of_the_same_key_returns_conflict_and_a_retry_reuses_the_document()
    {
        // Carrera determinista: el comprobante se inserta desde otra conexión DESPUÉS de que el handler
        // buscó la clave (sin encontrarla) y ANTES de su SaveChanges — el armado de la fila de una NC
        // consulta el repositorio de NC justo en ese intervalo.
        var creditNotes = new Mock<IPurchaseCreditNoteRepository>();
        creditNotes
            .Setup(r => r.GetIdByReceptionDocumentIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await using var competitor = CreateContext(_tenantId, _companyA);
                competitor.PurchaseReceptionDocuments.Add(PurchaseReceptionDocument.Create(
                    _tenantId, _companyA, _branchA, PurchaseReceptionSourceDocType.CreditNote,
                    "1791111111001", "PROVEEDOR S.A.", null, KeyRace, "001-001-000000001",
                    new DateOnly(2026, 7, 1), new DateTime(2026, 7, 1, 15, 0, 0, DateTimeKind.Utc),
                    10m, 1.5m, 11.5m, _userId, modifiedDocumentNumber: "001-001-000000001"));
                await competitor.SaveChangesAsync();
                return (Guid?)null;
            });

        await using (var db = CreateContext(_tenantId, _companyA))
        {
            var raced = await Handler(db, _companyA, _branchA, creditNotes.Object).Handle(
                Txt(RucA, ("Factura", KeyNew), ("Nota de Crédito", KeyRace)), CancellationToken.None);

            raced.IsSuccess.Should().BeFalse();
            raced.Error.Should().Be(ImportPurchaseReceptionHandler.AccessKeyConflictMessage);
        }

        var afterRace = await AllTenantDocumentsAsync();
        afterRace.Should().ContainSingle("solo queda la fila de la otra importación; el archivo se revirtió");
        afterRace[0].AccessKey.Should().Be(KeyRace);

        await using (var retryDb = CreateContext(_tenantId, _companyA))
        {
            var retry = await Handler(retryDb, _companyA, _branchA).Handle(
                Txt(RucA, ("Factura", KeyNew), ("Nota de Crédito", KeyRace)), CancellationToken.None);
            retry.IsSuccess.Should().BeTrue(retry.Error);
        }

        (await AllTenantDocumentsAsync()).Select(d => d.AccessKey)
            .Should().BeEquivalentTo([KeyNew, KeyRace]);
    }

    /// <summary>
    /// Otros fallos reales de PostgreSQL nunca se presentan como duplicado: una FK inexistente (23503)
    /// y un valor que excede la columna (22001) se propagan como error de persistencia, y el archivo no
    /// deja filas.
    /// </summary>
    [Fact]
    public async Task Foreign_key_violation_is_not_reported_as_a_duplicate()
    {
        await using var db = CreateContext(_tenantId, _companyA);

        var act = () => Handler(db, _companyA, branchId: Guid.NewGuid()).Handle(
            Txt(RucA, ("Factura", new string('8', 49))), CancellationToken.None);

        (await act.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<Npgsql.PostgresException>()
            .Which.SqlState.Should().Be("23503");
        (await AllTenantDocumentsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Value_too_long_is_not_reported_as_a_duplicate()
    {
        await using var db = CreateContext(_tenantId, _companyA);

        var act = () => Handler(db, _companyA, _branchA).Handle(
            Txt(RucA, ("Factura", new string('9', 60))), CancellationToken.None);

        (await act.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<Npgsql.PostgresException>()
            .Which.SqlState.Should().Be("22001");
        (await AllTenantDocumentsAsync()).Should().BeEmpty();
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

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
