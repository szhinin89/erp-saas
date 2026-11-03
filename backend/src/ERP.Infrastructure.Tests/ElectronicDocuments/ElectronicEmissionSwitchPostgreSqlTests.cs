using System.Reflection;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.ElectronicDocuments;
using ERP.Infrastructure.Services;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;

using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.ElectronicDocuments;

/// <summary>
/// SRI-TOGGLE-02/06 — interruptor <c>electronic_documents.emission_enabled</c> contra PostgreSQL real:
/// default OFF, aislamiento por TenantId+CompanyId, fecha de habilitación desde el historial auditado,
/// serialización FOR SHARE frente al apagado y filtro de candidatos del job de reintentos (pausa total:
/// también las consultas de autorización).
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class ElectronicEmissionSwitchPostgreSqlTests : IAsyncLifetime
{
    private const string Key = OrgSettingKeys.ElectronicDocuments.EmissionEnabled;

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_electronic_emission_switch_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = NewDbContext(_tenantId, _companyId);
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task Sin_valor_persistido_la_emision_esta_deshabilitada()
    {
        await using var db = NewDbContext(_tenantId, _companyId);

        var state = await new ElectronicEmissionSwitch(db).GetAsync(_tenantId, _companyId, false);

        state.Enabled.Should().BeFalse();
        state.EnabledSinceUtc.Should().BeNull();
    }

    [Theory]
    [InlineData("True", true)]
    [InlineData(" TRUE ", true)]
    [InlineData("False", false)]
    [InlineData("yes", false)]
    [InlineData("1", false)]
    public async Task Solo_un_valor_legible_como_true_habilita(string persisted, bool expected)
    {
        var writtenAt = DateTime.UtcNow.AddHours(-3);
        await using var db = NewDbContext(_tenantId, _companyId);
        await ElectronicEmissionTestSupport.SetAsync(db, _tenantId, _companyId, persisted, writtenAt, _userId);

        var state = await new ElectronicEmissionSwitch(db).GetAsync(_tenantId, _companyId, false);

        state.Enabled.Should().Be(expected);
        if (expected)
            state.EnabledSinceUtc.Should().BeCloseTo(writtenAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Habilitar_una_empresa_no_habilita_otra_empresa_ni_otro_tenant()
    {
        var otherCompany = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        await using var db = NewDbContext(_tenantId, _companyId);
        await ElectronicEmissionTestSupport.EnableSinceYesterdayAsync(db, _tenantId, _companyId, _userId);
        var emissionSwitch = new ElectronicEmissionSwitch(db);

        (await emissionSwitch.GetAsync(_tenantId, _companyId, false)).Enabled.Should().BeTrue();
        (await emissionSwitch.GetAsync(_tenantId, otherCompany, false)).Enabled.Should().BeFalse();
        (await emissionSwitch.GetAsync(otherTenant, _companyId, false)).Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task La_fecha_de_habilitacion_es_el_ultimo_cambio_auditado_a_true()
    {
        var firstEnable = DateTime.UtcNow.AddDays(-10);
        var reEnable = DateTime.UtcNow.AddHours(-1);
        await using var db = NewDbContext(_tenantId, _companyId);
        await ElectronicEmissionTestSupport.SetAsync(db, _tenantId, _companyId, "True", firstEnable, _userId);
        await AddChangeLogAsync(db, null, "True", firstEnable);
        await AddChangeLogAsync(db, "True", "False", DateTime.UtcNow.AddDays(-2));
        await AddChangeLogAsync(db, "False", "True", reEnable);

        var state = await new ElectronicEmissionSwitch(db).GetAsync(_tenantId, _companyId, false);

        state.EnabledSinceUtc.Should().BeCloseTo(reEnable, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task SRI_TOGGLE_03_ruta_auditada_real_el_corte_es_la_ultima_habilitacion_y_un_guardado_sin_cambio_no_lo_mueve()
    {
        await using var db = NewDbContext(_tenantId, _companyId);
        await ElectronicEmissionTestSupport.SetAuditedAsync(db, _tenantId, _companyId, true, _userId);
        await ElectronicEmissionTestSupport.SetAuditedAsync(db, _tenantId, _companyId, false, _userId);
        var beforeReEnable = DateTime.UtcNow;
        await ElectronicEmissionTestSupport.SetAuditedAsync(db, _tenantId, _companyId, true, _userId);
        var cutoff = (await new ElectronicEmissionSwitch(db).GetAsync(_tenantId, _companyId, false)).EnabledSinceUtc;

        await ElectronicEmissionTestSupport.SetAuditedAsync(db, _tenantId, _companyId, true, _userId); // no-op

        cutoff.Should().BeOnOrAfter(beforeReEnable.AddMilliseconds(-1));
        (await new ElectronicEmissionSwitch(db).GetAsync(_tenantId, _companyId, false))
            .EnabledSinceUtc.Should().Be(cutoff, "un guardado sin cambio no se audita ni mueve el corte");
        (await db.ConfigurationChangeLogs.CountAsync(l => l.Key == Key)).Should().Be(3);
    }

    [Fact]
    public async Task SRI_TOGGLE_03_historial_incoherente_con_el_valor_vigente_usa_la_escritura_de_la_fila()
    {
        // Historial: habilitada hace 10 días y apagada hace 1 día; la fila dice True por una escritura fuera
        // de la ruta auditada hace 1 hora. El corte NO puede retroceder a la habilitación de hace 10 días.
        var rowWrittenAt = DateTime.UtcNow.AddHours(-1);
        await using var db = NewDbContext(_tenantId, _companyId);
        await ElectronicEmissionTestSupport.SetAsync(db, _tenantId, _companyId, "True", rowWrittenAt, _userId);
        await AddChangeLogAsync(db, null, "True", DateTime.UtcNow.AddDays(-10));
        await AddChangeLogAsync(db, "True", "False", DateTime.UtcNow.AddDays(-1));

        var state = await new ElectronicEmissionSwitch(db).GetAsync(_tenantId, _companyId, false);

        state.Enabled.Should().BeTrue();
        state.EnabledSinceUtc.Should().BeCloseTo(rowWrittenAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task SRI_TOGGLE_03_registros_de_otra_empresa_otra_key_u_otro_scope_no_definen_el_corte()
    {
        var rowWrittenAt = DateTime.UtcNow.AddHours(-1);
        await using var db = NewDbContext(_tenantId, _companyId);
        await ElectronicEmissionTestSupport.SetAsync(db, _tenantId, _companyId, "True", rowWrittenAt, _userId);
        await AddLogAsync(db, _tenantId, Guid.NewGuid(), OrgScope.Company, Key, DateTime.UtcNow.AddDays(-5));
        await AddLogAsync(db, _tenantId, _companyId, OrgScope.Company,
            OrgSettingKeys.ElectronicDocuments.AutoRetryEnabled, DateTime.UtcNow.AddDays(-5));
        await AddLogAsync(db, _tenantId, _companyId, OrgScope.Branch, Key, DateTime.UtcNow.AddDays(-5));

        var state = await new ElectronicEmissionSwitch(db).GetAsync(_tenantId, _companyId, false);

        state.EnabledSinceUtc.Should().BeCloseTo(rowWrittenAt, TimeSpan.FromSeconds(1));
    }

    private async Task AddLogAsync(ErpDbContext db, Guid tenantId, Guid companyId, OrgScope scope, string key,
        DateTime changedAtUtc)
    {
        var log = ConfigurationChangeLog.Create(tenantId, companyId, scope,
            scope == OrgScope.Company ? companyId : Guid.NewGuid(), key, "OrgSetting", null, "Value", null, "True",
            ConfigurationChangeValueType.Bool, _userId, ConfigurationChangeSource.Api);
        db.ConfigurationChangeLogs.Add(log);
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE configuration_change_log SET changed_at_utc = {changedAtUtc} WHERE id = {log.Id}");
    }

    [Fact]
    public async Task El_reclamo_abierto_serializa_el_apagado()
    {
        await using (var seed = NewDbContext(_tenantId, _companyId))
            await ElectronicEmissionTestSupport.EnableSinceYesterdayAsync(seed, _tenantId, _companyId, _userId);

        // Reclamo abierto: lee el interruptor con el lock compartido dentro de su transacción.
        await using var claim = NewDbContext(_tenantId, _companyId);
        await using var claimTx = await claim.Database.BeginTransactionAsync();
        (await new ElectronicEmissionSwitch(claim).GetAsync(_tenantId, _companyId, true))
            .Enabled.Should().BeTrue();

        // El apagado por la ruta de producción no se confirma mientras el reclamo siga abierto.
        var disable = SetAuditedInNewContextAsync(_companyId, false);
        (await Task.WhenAny(disable, Task.Delay(TimeSpan.FromSeconds(1)))).Should().NotBe(disable);

        await claimTx.CommitAsync();
        await disable.WaitAsync(TimeSpan.FromSeconds(30));

        await using var next = NewDbContext(_tenantId, _companyId);
        await using var nextTx = await next.Database.BeginTransactionAsync();
        (await new ElectronicEmissionSwitch(next).GetAsync(_tenantId, _companyId, true))
            .Enabled.Should().BeFalse("un reclamo posterior al apagado ve el valor ya confirmado");
    }

    [Fact]
    public async Task SRI_TOGGLE_04_una_lectura_durante_una_habilitacion_en_curso_espera_y_ve_la_habilitacion()
    {
        // Habilitación en curso: lock exclusivo tomado y registro auditado fechado, sin commit todavía.
        await using var enabling = NewDbContext(_tenantId, _companyId);
        await using var enablingTx = await enabling.Database.BeginTransactionAsync();
        await new ElectronicEmissionSwitch(enabling).LockForChangeAsync(_tenantId, _companyId);
        await new ERP.Infrastructure.Persistence.Repositories.Configuration.OrgSettingsRepository(
                enabling, new ConfigurationChangeLogger(enabling))
            .UpsertAsync(OrgSetting.Create(_tenantId, _companyId, OrgScope.Company, _companyId, Key, "True",
                SettingDataType.Bool, _userId));
        await enabling.SaveChangesAsync();

        // Una lectura puntual (como el control del pipeline) no puede ver "deshabilitada" en esta ventana.
        var read = ReadInNewContextAsync(_companyId);
        (await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(1)))).Should().NotBe(read);

        // Aislamiento: otra empresa no espera por este lock.
        (await ReadInNewContextAsync(Guid.NewGuid()).WaitAsync(TimeSpan.FromSeconds(5)))
            .Enabled.Should().BeFalse();

        await enablingTx.CommitAsync();
        var state = await read.WaitAsync(TimeSpan.FromSeconds(30));
        state.Enabled.Should().BeTrue();
        var logged = await enabling.ConfigurationChangeLogs.AsNoTracking()
            .Where(l => l.Key == Key).Select(l => l.ChangedAtUtc).SingleAsync();
        state.EnabledSinceUtc.Should().BeCloseTo(logged, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Candidatos_del_job_solo_llegan_al_SRI_con_actividad_posterior_a_la_habilitacion()
    {
        var enabledAt = DateTime.UtcNow.AddHours(-2);
        var disabledCompany = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        await using (var seed = NewDbContext(_tenantId, _companyId))
            await ElectronicEmissionTestSupport.SetAsync(seed, _tenantId, _companyId, "True", enabledAt, _userId);

        var pendingBefore = await SeedDocumentAsync(_companyId, ElectronicDocumentState.Failed, DateTime.UtcNow.AddHours(-3));
        var failedAfter = await SeedDocumentAsync(_companyId, ElectronicDocumentState.Failed, DateTime.UtcNow.AddHours(-1));
        var draftAfter = await SeedDocumentAsync(_companyId, ElectronicDocumentState.Draft, DateTime.UtcNow.AddMinutes(-30));
        var receivedAfter = await SeedDocumentAsync(_companyId, ElectronicDocumentState.Received, DateTime.UtcNow.AddMinutes(-30));
        // SRI-TOGGLE-04 — Draft sin intentos y sin origen persistido: creado "ahora" (posterior a la
        // habilitación) y aun así no es candidato: la creación del comprobante nunca cuenta como actividad.
        var draftUnknownSource = await SeedDocumentAsync(_companyId, ElectronicDocumentState.Draft, null);
        var signedBefore = await SeedDocumentAsync(_companyId, ElectronicDocumentState.Signed, DateTime.UtcNow.AddHours(-3));
        var receivedBefore = await SeedDocumentAsync(_companyId, ElectronicDocumentState.Received, DateTime.UtcNow.AddHours(-3));
        var disabledDraft = await SeedDocumentAsync(disabledCompany, ElectronicDocumentState.Draft, DateTime.UtcNow.AddMinutes(-30));
        var disabledReceived = await SeedDocumentAsync(disabledCompany, ElectronicDocumentState.Received, DateTime.UtcNow.AddMinutes(-30));
        // Mismo CompanyId en otro tenant (no habilitado): la habilitación nunca cruza tenants.
        var otherTenantReceived = await SeedDocumentAsync(_companyId, ElectronicDocumentState.Received,
            DateTime.UtcNow.AddMinutes(-30), otherTenant);

        await using var job = NewDbContext(Guid.Empty, Guid.Empty);
        var candidates = (await new ElectronicDocumentRepository(job, Mock.Of<ICompanyClock>())
            .GetRetryCandidatesAsync(50)).Select(c => c.ElectronicDocumentId).ToList();

        candidates.Should().Contain([failedAfter, draftAfter, receivedAfter], "actividad posterior a la habilitación");
        candidates.Should().NotContain(pendingBefore, "pendiente previo: solo reanudación explícita");
        candidates.Should().NotContain([signedBefore, receivedBefore],
            "SRI-TOGGLE-06: tampoco se consulta automáticamente un pendiente anterior a la habilitación");
        candidates.Should().NotContain(draftUnknownSource, "sin actividad conocida no hay transmisión automática");
        candidates.Should().NotContain([disabledDraft, disabledReceived],
            "SRI-TOGGLE-06: empresa deshabilitada, ni transmisión ni consulta");
        candidates.Should().NotContain(otherTenantReceived, "aislamiento por TenantId+CompanyId");
    }

    [Fact]
    public async Task SRI_TOGGLE_06_apagar_retira_del_lote_todos_los_pendientes_y_rehabilitar_no_los_retoma()
    {
        // Habilitada hace 1 h; actividad hace 30 min (posterior y ya fuera del backoff de la política).
        await using (var seed = NewDbContext(_tenantId, _companyId))
            await ElectronicEmissionTestSupport.SetAsync(seed, _tenantId, _companyId, "True",
                DateTime.UtcNow.AddHours(-1), _userId);
        var received = await SeedDocumentAsync(_companyId, ElectronicDocumentState.Received, DateTime.UtcNow.AddMinutes(-30));
        var failed = await SeedDocumentAsync(_companyId, ElectronicDocumentState.Failed, DateTime.UtcNow.AddMinutes(-30));
        (await CandidatesAsync()).Should().Contain([received, failed]);

        await SetAuditedInNewContextAsync(_companyId, false);
        (await CandidatesAsync()).Should().NotContain([received, failed], "pausa total");

        await SetAuditedInNewContextAsync(_companyId, true);
        (await CandidatesAsync()).Should().NotContain([received, failed],
            "anteriores a la última habilitación: solo una acción explícita los reanuda");
    }

    private async Task<IReadOnlyList<Guid>> CandidatesAsync()
    {
        await using var job = NewDbContext(Guid.Empty, Guid.Empty);
        return (await new ElectronicDocumentRepository(job, Mock.Of<ICompanyClock>())
            .GetRetryCandidatesAsync(50)).Select(c => c.ElectronicDocumentId).ToList();
    }

    [Fact]
    public async Task SRI_TOGGLE_07_una_llamada_SRI_en_curso_retiene_el_apagado_y_ninguna_posterior_ve_habilitada()
    {
        var otherCompany = Guid.NewGuid();
        await using (var seed = NewDbContext(_tenantId, _companyId))
            await ElectronicEmissionTestSupport.EnableSinceYesterdayAsync(seed, _tenantId, _companyId, _userId);

        // Llamada SRI en curso (ámbito abierto, sin transacción: así se sostiene durante el SOAP).
        await using var inFlight = NewDbContext(_tenantId, _companyId);
        var emission = new ElectronicEmissionSwitch(inFlight);
        var call = await emission.EnterSriCallAsync(_tenantId, _companyId);
        (await emission.GetAsync(_tenantId, _companyId, false)).Enabled.Should().BeTrue();

        // Reentrante, y un reclamo (lock de transacción) del mismo backend no se autobloquea.
        await using (await emission.EnterSriCallAsync(_tenantId, _companyId))
        {
            await using var claim = await inFlight.Database.BeginTransactionAsync();
            (await emission.GetAsync(_tenantId, _companyId, true).WaitAsync(TimeSpan.FromSeconds(5)))
                .Enabled.Should().BeTrue();
            await claim.CommitAsync();
        }

        // El apagado no se confirma mientras la llamada siga en curso (tampoco tras el commit del reclamo).
        var disable = SetAuditedInNewContextAsync(_companyId, false);
        (await Task.WhenAny(disable, Task.Delay(TimeSpan.FromSeconds(1)))).Should().NotBe(disable);

        // Una llamada nueva queda detrás del apagado pendiente; otra empresa no espera.
        var late = EnterAndReadInNewContextAsync(_companyId);
        (await Task.WhenAny(late, Task.Delay(TimeSpan.FromSeconds(1)))).Should().NotBe(late);
        (await EnterAndReadInNewContextAsync(otherCompany).WaitAsync(TimeSpan.FromSeconds(5)))
            .Enabled.Should().BeFalse("otra empresa, sin valor: deshabilitada y sin esperar");

        await call.DisposeAsync();
        inFlight.Database.GetDbConnection().State.Should().Be(System.Data.ConnectionState.Closed,
            "el ámbito devuelve la conexión al salir");
        await disable.WaitAsync(TimeSpan.FromSeconds(30));
        (await late.WaitAsync(TimeSpan.FromSeconds(30))).Enabled
            .Should().BeFalse("ninguna llamada iniciada después de confirmar el apagado ve habilitada");
    }

    [Fact]
    public async Task SRI_TOGGLE_07_la_habilitacion_tambien_espera_a_las_llamadas_en_curso()
    {
        await using var inFlight = NewDbContext(_tenantId, _companyId);
        var emission = new ElectronicEmissionSwitch(inFlight);
        var call = await emission.EnterSriCallAsync(_tenantId, _companyId);
        (await emission.GetAsync(_tenantId, _companyId, false)).Enabled.Should().BeFalse();

        var enable = SetAuditedInNewContextAsync(_companyId, true);
        (await Task.WhenAny(enable, Task.Delay(TimeSpan.FromSeconds(1)))).Should().NotBe(enable);
        (await emission.GetAsync(_tenantId, _companyId, false)).Enabled
            .Should().BeFalse("el estado es estable durante toda la llamada");

        await call.DisposeAsync();
        await enable.WaitAsync(TimeSpan.FromSeconds(30));
        (await EnterAndReadInNewContextAsync(_companyId)).Enabled.Should().BeTrue();
    }

    private async Task<ERP.Application.Modules.ElectronicDocuments.Services.ElectronicEmissionState> EnterAndReadInNewContextAsync(
        Guid companyId)
    {
        await using var db = NewDbContext(_tenantId, companyId);
        var emission = new ElectronicEmissionSwitch(db);
        await using var call = await emission.EnterSriCallAsync(_tenantId, companyId);
        return await emission.GetAsync(_tenantId, companyId, false);
    }

    private async Task SetAuditedInNewContextAsync(Guid companyId, bool enabled)
    {
        await using var db = NewDbContext(_tenantId, companyId);
        await ElectronicEmissionTestSupport.SetAuditedAsync(db, _tenantId, companyId, enabled, _userId);
    }

    private async Task<ERP.Application.Modules.ElectronicDocuments.Services.ElectronicEmissionState> ReadInNewContextAsync(
        Guid companyId)
    {
        await using var db = NewDbContext(_tenantId, companyId);
        return await new ElectronicEmissionSwitch(db).GetAsync(_tenantId, companyId, false);
    }

    private async Task AddChangeLogAsync(ErpDbContext db, string? oldValue, string newValue, DateTime changedAtUtc)
    {
        var log = ConfigurationChangeLog.Create(_tenantId, _companyId, OrgScope.Company, _companyId, Key,
            "OrgSetting", null, "Value", oldValue, newValue, ConfigurationChangeValueType.Bool, _userId,
            ConfigurationChangeSource.Api);
        db.ConfigurationChangeLogs.Add(log);
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE configuration_change_log SET changed_at_utc = {changedAtUtc} WHERE id = {log.Id}");
    }

    private async Task<Guid> SeedDocumentAsync(Guid companyId, ElectronicDocumentState state, DateTime? lastAttemptUtc,
        Guid? tenantId = null)
    {
        var tenant = tenantId ?? _tenantId;
        await using var db = NewDbContext(tenant, companyId);
        var document = ElectronicDocument.Create(tenant, companyId, ElectronicDocumentType.Invoice,
            "Sales", Guid.NewGuid(), _userId);
        SetProperty(document, nameof(ElectronicDocument.CurrentState), state);
        SetProperty(document, nameof(ElectronicDocument.LastAttemptUtc), lastAttemptUtc);
        db.ElectronicDocuments.Add(document);
        await db.SaveChangesAsync();
        // CreatedAt anterior a cualquier habilitación: la actividad la marca LastAttemptUtc (o null = ahora).
        if (lastAttemptUtc is not null)
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE electronic_documents SET created_at = {lastAttemptUtc.Value.AddMinutes(-1)} WHERE id = {document.Id}");
        return document.Id;
    }

    private ErpDbContext NewDbContext(Guid tenantId, Guid companyId) =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options,
            new FixedCurrentTenant(tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(companyId)
        );

    private static void SetProperty(ElectronicDocument document, string propertyName, object? value)
    {
        for (var current = typeof(ElectronicDocument); current is not null; current = current.BaseType)
        {
            var property = current.GetProperty(propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (property is not null)
            {
                property.SetMethod!.Invoke(document, [value]);
                return;
            }
        }
        throw new InvalidOperationException($"Propiedad {propertyName} no encontrada.");
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

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
