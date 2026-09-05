using ERP.Application.Common;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// ZH-AUTH-DOCUMENT-SEQUENCE-COMPANY-SQL-SCOPE-09 — <c>DocumentSequenceRepository</c> usa SQL
/// raw/advisory-lock que bypassea los query filters globales de EF (<c>AsPlatformQuery()</c>,
/// justificado en <see cref="IgnoreQueryFiltersAuditTests"/>). Hasta esta fase, el predicado
/// interno del SELECT solo filtraba por (EmissionPointId, DocTypeCode) — un tenantId/companyId
/// inconsistente pasado por el caller no era detectado por el propio repositorio, dependiendo
/// enteramente de que el caller ya hubiera validado el ownership del EmissionPointId (ver
/// ZH-AUTH-MASTERDATA-REPOSITORY-COMPANY-SCOPE-07A). Esta suite prueba la defensa añadida:
/// CaptureNextAsync/GetForUpdateAsync ahora exigen la clave lógica completa
/// (TenantId + CompanyId + EmissionPointId + DocTypeCode) en el propio SQL, y fallan cerrado en
/// vez de crear una fila duplicada bajo un scope equivocado cuando ya existe una secuencia real
/// para ese EmissionPointId+DocTypeCode bajo otra empresa.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class DocumentSequenceCompanyScopeTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_docseq_companyscope_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyAId;
    private Guid _companyBId;
    private Guid _establishmentAId;
    private Guid _createdBy;
    private int _epCodeCounter;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext(Guid.Empty);
        await db.Database.MigrateAsync();

        _createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _createdBy);
        var companyA = Company.CreateManaged(
            tenant.Id,
            $"179{Guid.NewGuid():N}"[..13],
            "Empresa A S.A.",
            createdBy: _createdBy
        );
        var companyB = Company.CreateManaged(
            tenant.Id,
            $"179{Guid.NewGuid():N}"[..13],
            "Empresa B S.A.",
            createdBy: _createdBy
        );
        db.Tenants.Add(tenant);
        db.Companies.AddRange(companyA, companyB);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyAId = companyA.Id;
        _companyBId = companyB.Id;

        var establishmentA = Establishment.Create(
            _tenantId,
            branchId: null,
            _companyAId,
            code: "001",
            name: "Matriz A",
            address: "Av. Principal 123",
            phone: null,
            isMain: true,
            createdBy: _createdBy
        );
        db.Establishments.Add(establishmentA);
        await db.SaveChangesAsync();
        _establishmentAId = establishmentA.Id;
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    /// <summary>El EmissionPoint pertenece siempre a la Empresa A — la "inconsistencia" en cada
    /// test es que se invoca al repositorio con CompanyB para ese mismo punto de emisión.</summary>
    private async Task<Guid> CreateEmissionPointOfCompanyAAsync()
    {
        var counter = Interlocked.Increment(ref _epCodeCounter);
        var code = counter.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);

        await using var db = CreateContext(_companyAId);
        var ep = EmissionPoint.Create(
            _tenantId,
            _companyAId,
            _establishmentAId,
            code: code,
            name: $"EP-{code}",
            emissionType: EmissionType.Electronic,
            isDefault: false,
            createdBy: _createdBy
        );
        db.EmissionPoints.Add(ep);
        await db.SaveChangesAsync();
        return ep.Id;
    }

    // ── Caso 1: captura válida con tenant/company/emissionPoint/docType consistentes ──────────

    [Fact]
    public async Task CaptureNextAsync_con_scope_consistente_funciona_normalmente()
    {
        var epId = await CreateEmissionPointOfCompanyAAsync();
        await using var db = CreateContext(_companyAId);
        var repo = new DocumentSequenceRepository(db);

        var first = await repo.CaptureNextAsync(_tenantId, _companyAId, epId, "01");
        var second = await repo.CaptureNextAsync(_tenantId, _companyAId, epId, "01");

        first.Should().Be("000000001");
        second.Should().Be("000000002");
    }

    // ── Caso 2: no debe crear ni actualizar una secuencia de otra company cuando ya existe una
    // real para el mismo EmissionPointId+DocTypeCode bajo la company correcta ─────────────────

    [Fact]
    public async Task CaptureNextAsync_con_companyId_inconsistente_sobre_secuencia_existente_falla_cerrado()
    {
        var epId = await CreateEmissionPointOfCompanyAAsync();

        // Primero, una captura legítima bajo la empresa dueña real (Company A) — crea la fila.
        await using (var db = CreateContext(_companyAId))
        {
            var repo = new DocumentSequenceRepository(db);
            var first = await repo.CaptureNextAsync(_tenantId, _companyAId, epId, "01");
            first.Should().Be("000000001");
        }

        // Ahora, el mismo EmissionPointId+DocTypeCode pero con CompanyB (inconsistente: ese punto
        // de emisión nunca perteneció a Company B) — debe fallar cerrado, nunca crear una segunda
        // fila fantasma para el mismo punto de emisión bajo otro scope.
        await using (var db = CreateContext(_companyBId))
        {
            var repo = new DocumentSequenceRepository(db);
            var act = async () =>
                await repo.CaptureNextAsync(_tenantId, _companyBId, epId, "01");

            await act.Should()
                .ThrowAsync<InvalidOperationException>(
                    "un companyId inconsistente con el dueño real de la secuencia debe fallar cerrado, "
                        + "nunca crear una fila duplicada bajo otro scope"
                );
        }

        // La fila real bajo Company A queda intacta (no se corrompió por el intento fallido).
        await using var verifyDb = CreateContext(_companyAId);
        var repoVerify = new DocumentSequenceRepository(verifyDb);
        var stillOwnedByA = await repoVerify.GetForUpdateAsync(_tenantId, _companyAId, epId, "01");
        stillOwnedByA.Should().NotBeNull();
        stillOwnedByA!.CurrentSeq.Should().Be(2);
        stillOwnedByA.CompanyId.Should().Be(_companyAId);

        // Y no existe ninguna fila fantasma bajo Company B para ese mismo punto de emisión.
        var phantomUnderB = await repoVerify.GetForUpdateAsync(_tenantId, _companyBId, epId, "01");
        phantomUnderB.Should().BeNull();
    }

    // ── Caso 3: primera captura jamás realizada (sin fila previa) con companyId inconsistente —
    // límite documentado: sin JOIN a emission_points, el repositorio no puede detectar por sí
    // solo que el EmissionPointId pertenece a otra empresa si nunca hubo una fila previa; esa
    // primera captura depende del caller (ver ConfigureDocumentSequenceCommandHandler/
    // AuthorizeSalesUseCases, que ya validan ownership de EmissionPointId antes de llamar aquí,
    // ZH-AUTH-MASTERDATA-REPOSITORY-COMPANY-SCOPE-07A). Se documenta explícitamente en vez de
    // fingir una garantía que requeriría un JOIN evitado a propósito por esta fase.

    [Fact]
    public async Task CaptureNextAsync_primera_captura_nunca_realizada_confia_en_el_ownership_ya_validado_por_el_caller()
    {
        var epId = await CreateEmissionPointOfCompanyAAsync();

        // Sin fila previa para (epId, "05") en ninguna empresa: el repositorio no tiene forma de
        // detectar, sin un JOIN a emission_points, que epId realmente pertenece a Company A —
        // por diseño, la primera captura confía en que el caller ya validó esa pertenencia.
        await using var db = CreateContext(_companyBId);
        var repo = new DocumentSequenceRepository(db);

        var captured = await repo.CaptureNextAsync(_tenantId, _companyBId, epId, "05");

        captured.Should().Be("000000001");
    }

    // ── Caso 4: GetForUpdateAsync respeta company ──────────────────────────────────────────────

    [Fact]
    public async Task GetForUpdateAsync_con_company_correcta_encuentra_la_fila()
    {
        var epId = await CreateEmissionPointOfCompanyAAsync();
        await using (var db = CreateContext(_companyAId))
        {
            var repo = new DocumentSequenceRepository(db);
            await repo.CaptureNextAsync(_tenantId, _companyAId, epId, "01");
        }

        await using var verifyDb = CreateContext(_companyAId);
        var verifyRepo = new DocumentSequenceRepository(verifyDb);
        var found = await verifyRepo.GetForUpdateAsync(_tenantId, _companyAId, epId, "01");

        found.Should().NotBeNull();
        found!.EmissionPointId.Should().Be(epId);
    }

    [Fact]
    public async Task GetForUpdateAsync_con_otra_company_no_encuentra_la_fila()
    {
        var epId = await CreateEmissionPointOfCompanyAAsync();
        await using (var db = CreateContext(_companyAId))
        {
            var repo = new DocumentSequenceRepository(db);
            await repo.CaptureNextAsync(_tenantId, _companyAId, epId, "01");
        }

        await using var verifyDb = CreateContext(_companyBId);
        var verifyRepo = new DocumentSequenceRepository(verifyDb);
        var found = await verifyRepo.GetForUpdateAsync(_tenantId, _companyBId, epId, "01");

        found.Should().BeNull();
    }

    // ── Caso 5: no se introduce BranchId en ninguna parte de la clave ──────────────────────────

    [Fact]
    public async Task CaptureNextAsync_no_requiere_ni_expone_BranchId()
    {
        // Confirmación de contrato: la firma pública de CaptureNextAsync/GetForUpdateAsync no
        // incluye BranchId — la clave lógica sigue siendo exclusivamente
        // TenantId + CompanyId + EmissionPointId + DocTypeCode.
        var epId = await CreateEmissionPointOfCompanyAAsync();
        await using var db = CreateContext(_companyAId);
        var repo = new DocumentSequenceRepository(db);

        var captured = await repo.CaptureNextAsync(_tenantId, _companyAId, epId, "01");

        captured.Should().Be("000000001");
    }

    // ── Infraestructura ──────────────────────────────────────────────────────────────────────

    private ErpDbContext CreateContext(Guid ambientCompanyId)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(ambientCompanyId)
        );
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
        public bool HasCompanyContext => true;
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
