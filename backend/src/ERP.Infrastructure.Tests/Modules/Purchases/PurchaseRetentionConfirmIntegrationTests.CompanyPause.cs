using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Infrastructure.Services;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests.Modules.Purchases;

/// <summary>
/// ADR-048 / SEC-SUSP-01 con retenciones contra PostgreSQL real y el interruptor REAL: (1) el descubrimiento separa la
/// TRANSMISIÓN NUEVA (retención sin comprobante: pausada) de la CONCILIACIÓN (comprobante de un envío previo o
/// incierto: continúa); (3) el reclamo guardado (<c>ClaimDispatchAsync</c>, con la fila de la empresa FOR SHARE y su
/// transacción real) bloquea una suspensión ocurrida entre la comprobación inicial y el reclamo, sin llamar al SRI,
/// sin escrituras, sin intentos consumidos ni DeadLetter. Solo se simula la frontera SRI (<see cref="SriBoundaryDouble"/>).
/// </summary>
public sealed partial class PurchaseRetentionConfirmIntegrationTests
{
    private async Task SetCompanyStateAsync(bool isActive, CompanyOperationalStatus status)
    {
        await using var db = CreateContext();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE company SET is_active = {isActive}, operational_status = {(int)status} WHERE id = {_companyId}");
    }

    // ── 1. Descubrimiento de retenciones ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(true, CompanyOperationalStatus.Suspended)]
    [InlineData(false, CompanyOperationalStatus.Operational)]
    [InlineData(false, CompanyOperationalStatus.Suspended)]
    public async Task SEC_SUSP_01_el_descubrimiento_no_ofrece_la_transmision_inicial_de_una_empresa_pausada(
        bool isActive, CompanyOperationalStatus status)
    {
        var (_, retentionId) = await ConfirmWithIssuedRetentionAsync();
        (await RecoveryCandidatesAsync()).Should().Contain(c => c.RetentionId == retentionId, "operativa: es candidata");

        await SetCompanyStateAsync(isActive, status);
        (await RecoveryCandidatesAsync()).Should().NotContain(c => c.RetentionId == retentionId);

        await SetCompanyStateAsync(true, CompanyOperationalStatus.Operational);
        (await RecoveryCandidatesAsync()).Should().Contain(c => c.RetentionId == retentionId, "reanuda sin pérdida");
    }

    [Fact]
    public async Task SEC_SUSP_01_PendingSetup_conserva_el_descubrimiento()
    {
        var (_, retentionId) = await ConfirmWithIssuedRetentionAsync();
        await SetCompanyStateAsync(true, CompanyOperationalStatus.PendingSetup);

        (await RecoveryCandidatesAsync()).Should().Contain(c => c.RetentionId == retentionId);
    }

    [Fact]
    public async Task SEC_SUSP_01_empresa_pausada_pausa_el_Draft_pero_concilia_el_envio_incierto()
    {
        await using (var seed = CreateContext())
            await ElectronicEmissionTestSupport.EnableSinceYesterdayAsync(seed, _tenantId, _companyId, _userId);
        var (_, draftRetention) = await ConfirmWithIssuedRetentionAsync();
        var (_, receivedRetention) = await ConfirmWithIssuedRetentionAsync();
        var draft = await SeedElectronicDocumentAsync(draftRetention, ElectronicDocumentState.Draft);
        var received = await SeedElectronicDocumentAsync(receivedRetention, ElectronicDocumentState.Received);
        await using (var db = CreateContext())
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE electronic_documents SET last_attempt_utc = {DateTime.UtcNow.AddHours(-1)} WHERE id = {received}");
        await SetCompanyStateAsync(true, CompanyOperationalStatus.Suspended);

        var candidates = await RetryCandidateIdsAsync();
        candidates.Should().Contain(received, "la conciliación de un envío incierto continúa");
        candidates.Should().NotContain(draft, "una transmisión nueva no es candidata");

        var sri = new SriBoundaryDouble(_companyId);
        // Proceso automático: el Draft no llama al SRI; el Received solo CONSULTA (nunca reenvía).
        (await RetryWithRealSwitchAsync(draft, Guid.Empty, sri)).Code
            .Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        sri.SendCalls.Should().Be(0);
        sri.AuthorizationCalls.Should().Be(0);

        await RetryWithRealSwitchAsync(received, Guid.Empty, sri);
        sri.AuthorizationCalls.Should().Be(1, "consulta de autorización del envío previo");
        sri.SendCalls.Should().Be(0, "nunca se reenvía");
    }

    // ── 3. Reclamo guardado dentro de la operación real ───────────────────────────────────────

    /// <summary>
    /// Decorador del interruptor REAL: tras la N-ésima lectura SIN lock (las comprobaciones previas al reclamo) ejecuta
    /// la suspensión de la empresa, de modo que la lectura del RECLAMO (FOR SHARE, en su transacción) ya la ve.
    /// </summary>
    private sealed class SuspendAfterReadsSwitch(
        IElectronicEmissionSwitch inner, int nonLockReads, Func<Task> suspend) : IElectronicEmissionSwitch
    {
        private int _reads;
        public List<bool> LockedReadSawPause { get; } = [];

        public async Task<ElectronicEmissionState> GetAsync(Guid tenantId, Guid companyId, bool lockForShare, CancellationToken ct = default)
        {
            var state = await inner.GetAsync(tenantId, companyId, lockForShare, ct);
            if (lockForShare)
                LockedReadSawPause.Add(state.CompanyPaused);
            else if (++_reads == nonLockReads)
                await suspend();
            return state;
        }

        public Task<DateTime?> GetSourceActivityUtcAsync(Guid tenantId, Guid companyId, string sourceModule,
            ElectronicDocumentType documentType, Guid sourceEntityId, CancellationToken ct = default) =>
            inner.GetSourceActivityUtcAsync(tenantId, companyId, sourceModule, documentType, sourceEntityId, ct);

        public Task<IAsyncDisposable> EnterSriCallAsync(Guid tenantId, Guid companyId, CancellationToken ct = default) =>
            inner.EnterSriCallAsync(tenantId, companyId, ct);

        public Task LockForChangeAsync(Guid tenantId, Guid companyId, CancellationToken ct = default) =>
            inner.LockForChangeAsync(tenantId, companyId, ct);
    }

    [Fact]
    public async Task SEC_SUSP_01_el_reclamo_guardado_bloquea_una_suspension_posterior_a_la_comprobacion_inicial()
    {
        await SetEmissionAuditedAsync(true);
        var (_, retentionId) = await ConfirmWithIssuedRetentionAsync();
        var sri = new SriBoundaryDouble(_companyId);

        Result<ERP.Application.Modules.ElectronicDocuments.DTOs.ElectronicDocumentDto> result;
        SuspendAfterReadsSwitch sw;
        await using (var db = CreateContext())
        {
            // La comprobación del pipeline pasa («operativa»); la empresa se suspende; el reclamo FOR SHARE la ve.
            sw = new SuspendAfterReadsSwitch(new ElectronicEmissionSwitch(db), nonLockReads: 1,
                suspend: () => SetCompanyStateAsync(true, CompanyOperationalStatus.Suspended));
            result = await RetentionElectronicTestWiring
                .Transmission(db, new FixedCurrentCompany(_companyId), sri, sw)
                .StartAsync(_tenantId, _companyId, retentionId, _userId);
            db.Database.CurrentTransaction.Should().BeNull("el reclamo hizo rollback: sin transacción abierta");
        }

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        sw.LockedReadSawPause.Should().ContainSingle().Which.Should().BeTrue("el reclamo ve la suspensión");
        sri.SendCalls.Should().Be(0, "ninguna llamada externa");
        sri.AuthorizationCalls.Should().Be(0);

        var document = await DocumentOfAsync(retentionId);
        document.CurrentState.Should().Be(ElectronicDocumentState.Draft, "rollback: nada de Signed/Dispatching persistido");
        document.AccessKey.Should().BeNull();
        document.RetryCount.Should().Be(0, "sin intentos consumidos");
        document.CurrentState.Should().NotBe(ElectronicDocumentState.DeadLetter);

        // La fila de la empresa quedó libre (sin lock colgado) y la reanudación transmite una sola vez.
        await SetCompanyStateAsync(true, CompanyOperationalStatus.Operational).WaitAsync(TimeSpan.FromSeconds(15));
        (await RetryWithRealSwitchAsync(document.Id, _userId, sri)).IsSuccess.Should().BeTrue();
        sri.SendCalls.Should().Be(1);
        var resumed = await DocumentOfAsync(retentionId);
        resumed.CurrentState.Should().NotBe(ElectronicDocumentState.Draft);
    }

    [Fact]
    public async Task SEC_SUSP_01_el_reclamo_bloqueado_de_un_Failed_agotado_no_pasa_a_DeadLetter()
    {
        await SetEmissionAuditedAsync(true);
        var (_, retentionId) = await ConfirmWithIssuedRetentionAsync();
        var failed = await SeedElectronicDocumentAsync(retentionId, ElectronicDocumentState.Failed);
        await using (var db = CreateContext())
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE electronic_documents SET retry_count = 50 WHERE id = {failed}");
        var sri = new SriBoundaryDouble(_companyId);

        Result<ERP.Application.Modules.ElectronicDocuments.DTOs.ElectronicDocumentDto> result;
        await using (var db = CreateContext())
        {
            // Retry del Failed: lecturas sin lock #1 (RetryGuardedAsync) y #2 (pipeline); suspensión tras la #2.
            var sw = new SuspendAfterReadsSwitch(new ElectronicEmissionSwitch(db), nonLockReads: 2,
                suspend: () => SetCompanyStateAsync(true, CompanyOperationalStatus.Suspended));
            result = await RetentionElectronicTestWiring
                .Issuer(db, new FixedCurrentCompany(_companyId), sri, sw)
                .RetryAsync(_tenantId, failed, _userId);
        }

        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        sri.SendCalls.Should().Be(0);
        var document = await DocumentOfAsync(retentionId);
        document.CurrentState.Should().Be(ElectronicDocumentState.Failed, "exhausto, pero la pausa no lo cierra por exhaución");
        document.RetryCount.Should().Be(50, "sin intentos consumidos ni perdidos");
    }
}
