using ERP.Application.Modules.Integration.UseCases;
using ERP.Domain.Modules.Communications.Constants;
using ERP.Domain.Modules.Communications.Entities;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Communications.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Infrastructure.Communications;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests.Accounting;

/// <summary>
/// ADR-048 / SEC-SUSP-01 sobre PostgreSQL real (fixture desechable): estado de pausa en la lectura del interruptor,
/// serialización entre un reclamo de envío y una suspensión concurrente, y pausa de la entrega de correo
/// (Pending intacto, sin intento ni <c>retry_count</c>; alcance System sin cambios; reanudación). Sin SRI ni SMTP.
/// </summary>
public sealed class CompanyPauseAutomaticProcessingPostgreSqlTests : IClassFixture<AccountingManual08Fixture>
{
    private const string Purpose = "SALES_INVOICE_AUTHORIZED";
    private readonly AccountingManual08Fixture _fixture;

    public CompanyPauseAutomaticProcessingPostgreSqlTests(AccountingManual08Fixture fixture) => _fixture = fixture;

    private static async Task SetStateAsync(ManualBook book, bool isActive, CompanyOperationalStatus status)
    {
        await using var db = book.Db();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE company SET is_active = {isActive}, operational_status = {(int)status} WHERE id = {book.CompanyId}");
    }

    private static async Task<CompanyPauseReason> PauseOfAsync(ManualBook book, bool lockForShare)
    {
        await using var db = book.Db();
        var @switch = new ElectronicEmissionSwitch(db);
        if (!lockForShare)
            return (await @switch.GetAsync(book.TenantId, book.CompanyId, false)).CompanyPause;
        await using var tx = await db.Database.BeginTransactionAsync();
        var state = await @switch.GetAsync(book.TenantId, book.CompanyId, true);
        await tx.CommitAsync();
        return state.CompanyPause;
    }

    // ── Lectura del estado ────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(true, CompanyOperationalStatus.Operational, CompanyPauseReason.None)]
    [InlineData(true, CompanyOperationalStatus.PendingSetup, CompanyPauseReason.None)]
    [InlineData(true, CompanyOperationalStatus.Suspended, CompanyPauseReason.Suspended)]
    [InlineData(false, CompanyOperationalStatus.Operational, CompanyPauseReason.Inactive)]
    [InlineData(false, CompanyOperationalStatus.Suspended, CompanyPauseReason.Suspended | CompanyPauseReason.Inactive)]
    public async Task La_lectura_del_interruptor_informa_el_motivo_de_pausa(
        bool isActive, CompanyOperationalStatus status, CompanyPauseReason expected)
    {
        var book = await _fixture.BookAsync();
        await SetStateAsync(book, isActive, status);

        (await PauseOfAsync(book, lockForShare: false)).Should().Be(expected);
        (await PauseOfAsync(book, lockForShare: true)).Should().Be(expected, "el reclamo lee lo mismo");
    }

    [Fact]
    public async Task Reanudar_exige_restablecer_ambos_estados()
    {
        var book = await _fixture.BookAsync();
        await SetStateAsync(book, false, CompanyOperationalStatus.Suspended);

        await SetStateAsync(book, true, CompanyOperationalStatus.Suspended);
        (await PauseOfAsync(book, false)).Should().Be(CompanyPauseReason.Suspended);

        await SetStateAsync(book, false, CompanyOperationalStatus.Operational);
        (await PauseOfAsync(book, false)).Should().Be(CompanyPauseReason.Inactive);

        await SetStateAsync(book, true, CompanyOperationalStatus.Operational);
        (await PauseOfAsync(book, false)).Should().Be(CompanyPauseReason.None);
    }

    // ── Suspensión concurrente con un reclamo de envío ───────────────────────────────────────

    [Fact]
    public async Task Una_suspension_espera_al_reclamo_abierto_y_el_reclamo_siguiente_ya_ve_la_pausa()
    {
        var book = await _fixture.BookAsync();
        await ResetOutboxAsync(book);

        // Reclamo en curso: lee «operativa» y mantiene la fila de la empresa FOR SHARE hasta su commit.
        await using var claimDb = book.Db();
        await using var claimTx = await claimDb.Database.BeginTransactionAsync();
        var duringClaim = await new ElectronicEmissionSwitch(claimDb).GetAsync(book.TenantId, book.CompanyId, true);
        duringClaim.CompanyPause.Should().Be(CompanyPauseReason.None);

        // Suspensión real (handler de Integración, sin modificar) en paralelo.
        await using var suspendDb = book.Db();
        var suspend = Task.Run(async () =>
            await new SuspendIntegrationCompanyHandler(new CompanyRepository(suspendDb))
                .Handle(new SuspendIntegrationCompanyCommand(book.CompanyId), CancellationToken.None));

        (await Task.WhenAny(suspend, Task.Delay(1500))).Should().NotBeSameAs(suspend,
            "la suspensión no se confirma mientras el reclamo mantiene la fila de la empresa");

        await claimTx.CommitAsync();
        (await suspend.WaitAsync(TimeSpan.FromSeconds(15))).IsSuccess.Should().BeTrue();

        // Ningún reclamo nuevo puede ver «operativa» una vez confirmada la suspensión.
        (await PauseOfAsync(book, lockForShare: true)).Should().Be(CompanyPauseReason.Suspended);
    }

    // ── Entrega de correo ─────────────────────────────────────────────────────────────────────

    private static async Task ResetOutboxAsync(ManualBook book)
    {
        // BD desechable y pruebas secuenciales de la clase: cada una parte sin correos ajenos.
        await using var db = book.Db();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE communication_outbox CASCADE");
    }

    private static async Task<Guid> SeedCommunicationAsync(ManualBook book, bool system = false)
    {
        await using var db = book.Db();
        var scope = system ? CommunicationScope.System : CommunicationScope.Company(book.TenantId, book.CompanyId);
        var purpose = system ? CommunicationPurposes.PasswordReset : Purpose;
        var row = CommunicationOutbox.CreateEmail(
            CommunicationIdentity.For(
                scope, purpose, CommunicationChannel.Email,
                new CommunicationSource(system ? "System" : "Sales", "Test", Guid.NewGuid()),
                system ? CommunicationRecipientRole.User : CommunicationRecipientRole.Customer),
            null, "cliente@test.com",
            new CommunicationTemplateUsage(purpose, 1, CommunicationTemplateSource.Default),
            "asunto", "<p>x</p>", null, CommunicationPriority.Normal,
            DateTime.UtcNow.AddMinutes(-5), 3, Guid.Empty);
        db.CommunicationOutbox.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    private static async Task<CommunicationOutbox> RowAsync(ManualBook book, Guid id)
    {
        await using var db = book.Db();
        return await db.CommunicationOutbox.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == id);
    }

    private static async Task<int> AttemptsAsync(ManualBook book, Guid id)
    {
        await using var db = book.Db();
        return await db.CommunicationDeliveryAttempts.IgnoreQueryFilters().CountAsync(a => a.CommunicationId == id);
    }

    /// <summary>Reclama hasta que no quede nada elegible; devuelve los ids reclamados de ESTA prueba.</summary>
    private static async Task<List<Guid>> ClaimAllAsync(ManualBook book, params Guid[] mine)
    {
        var claimed = new List<Guid>();
        await using var db = book.Db();
        var store = new CommunicationOutboxDeliveryStore(db);
        for (var i = 0; i < 20; i++)
        {
            var claim = await store.ClaimNextAsync(DateTime.UtcNow, TimeSpan.FromMinutes(5));
            if (claim is null)
                break;
            if (mine.Contains(claim.Id))
                claimed.Add(claim.Id);
        }
        return claimed;
    }

    [Theory]
    [InlineData(true, CompanyOperationalStatus.Suspended)]
    [InlineData(false, CompanyOperationalStatus.Operational)]
    [InlineData(false, CompanyOperationalStatus.Suspended)]
    public async Task Correo_de_empresa_pausada_permanece_Pending_sin_intento_ni_retry_count(
        bool isActive, CompanyOperationalStatus status)
    {
        var book = await _fixture.BookAsync();
        await ResetOutboxAsync(book);
        var id = await SeedCommunicationAsync(book);
        await SetStateAsync(book, isActive, status);

        (await ClaimAllAsync(book, id)).Should().BeEmpty();

        var row = await RowAsync(book, id);
        row.Status.Should().Be(CommunicationStatus.Pending);
        row.RetryCount.Should().Be(0);
        (await AttemptsAsync(book, id)).Should().Be(0);
    }

    [Fact]
    public async Task Correo_Processing_con_lease_vencido_de_empresa_pausada_no_se_recupera()
    {
        var book = await _fixture.BookAsync();
        await ResetOutboxAsync(book);
        var id = await SeedCommunicationAsync(book);
        var claimedBefore = await ClaimAllAsync(book, id); // operativa: lo reclama (Processing)
        claimedBefore.Should().ContainSingle();
        await using (var db = book.Db())
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE communication_outbox SET lease_until_utc = {DateTime.UtcNow.AddMinutes(-1)} WHERE id = {id}");
        var retries = (await RowAsync(book, id)).RetryCount;
        await SetStateAsync(book, true, CompanyOperationalStatus.Suspended);

        (await ClaimAllAsync(book, id)).Should().BeEmpty();

        var row = await RowAsync(book, id);
        row.Status.Should().Be(CommunicationStatus.Processing);
        row.RetryCount.Should().Be(retries);
    }

    [Fact]
    public async Task Correo_System_no_depende_de_ninguna_empresa()
    {
        var book = await _fixture.BookAsync();
        await ResetOutboxAsync(book);
        var system = await SeedCommunicationAsync(book, system: true);
        await SetStateAsync(book, false, CompanyOperationalStatus.Suspended);

        (await ClaimAllAsync(book, system)).Should().ContainSingle().Which.Should().Be(system);
    }

    [Fact]
    public async Task Correo_de_empresa_operativa_o_PendingSetup_se_reclama()
    {
        var operational = await _fixture.BookAsync();
        await ResetOutboxAsync(operational);
        var pending = await _fixture.BookAsync();
        var a = await SeedCommunicationAsync(operational);
        (await ClaimAllAsync(operational, a)).Should().ContainSingle();

        var b = await SeedCommunicationAsync(pending);
        await SetStateAsync(pending, true, CompanyOperationalStatus.PendingSetup);
        (await ClaimAllAsync(pending, b)).Should().ContainSingle("PendingSetup conserva su comportamiento");
    }

    [Fact]
    public async Task Al_reactivar_el_correo_pausado_se_entrega_una_sola_vez_sin_perdida()
    {
        var book = await _fixture.BookAsync();
        await ResetOutboxAsync(book);
        var id = await SeedCommunicationAsync(book);
        await SetStateAsync(book, false, CompanyOperationalStatus.Suspended);
        (await ClaimAllAsync(book, id)).Should().BeEmpty();

        await SetStateAsync(book, true, CompanyOperationalStatus.Operational);

        (await ClaimAllAsync(book, id)).Should().ContainSingle().Which.Should().Be(id);
        (await ClaimAllAsync(book, id)).Should().BeEmpty("ya está Processing: no se duplica");
        (await AttemptsAsync(book, id)).Should().Be(1);
        (await RowAsync(book, id)).RetryCount.Should().Be(0);
    }

    [Fact]
    public async Task La_observabilidad_informa_empresa_cantidad_antiguedad_y_motivo_sin_datos_personales()
    {
        var book = await _fixture.BookAsync();
        await ResetOutboxAsync(book);
        await SeedCommunicationAsync(book);
        await SeedCommunicationAsync(book);
        await SetStateAsync(book, false, CompanyOperationalStatus.Suspended);

        await using var db = book.Db();
        var rows = await new CommunicationOutboxDeliveryStore(db).GetPausedPendingAsync(50, CancellationToken.None);

        var row = rows.Should().ContainSingle(r => r.CompanyId == book.CompanyId).Subject;
        row.Count.Should().Be(2);
        row.Oldest.Should().NotBeNull();
        row.Inactive.Should().BeTrue();
        row.Suspended.Should().BeTrue();
        typeof(CommunicationOutboxDeliveryStore.PausedCommunicationRow).GetProperties().Select(p => p.Name)
            .Should().BeEquivalentTo("CompanyId", "Count", "Oldest", "Inactive", "Suspended");
    }
}
