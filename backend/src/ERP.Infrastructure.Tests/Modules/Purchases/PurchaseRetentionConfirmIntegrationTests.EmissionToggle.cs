using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Infrastructure.Persistence.Repositories.ElectronicDocuments;
using ERP.Infrastructure.Persistence.Services;
using ERP.Infrastructure.Services;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests.Modules.Purchases;

/// <summary>
/// SRI-TOGGLE-04 — reanudación automática de retenciones frente al interruptor de emisión, contra PostgreSQL
/// real con el interruptor REAL escrito por la ruta de producción (transacción + lock exclusivo + upsert
/// auditado). La decisión automática usa el instante persistido de emisión de la retención, nunca la
/// creación del comprobante: un Draft creado por la recuperación DESPUÉS de habilitar no convierte en
/// "posterior" una retención emitida antes. Solo se simula la frontera SRI (<see cref="SriBoundaryDouble"/>).
/// </summary>
public sealed partial class PurchaseRetentionConfirmIntegrationTests
{
    [Fact]
    public async Task SRI_TOGGLE_04_recuperacion_distingue_retenciones_anteriores_y_posteriores_a_la_habilitacion()
    {
        var (_, before) = await ConfirmWithIssuedRetentionAsync();
        await SetEmissionAuditedAsync(true);
        var (_, after) = await ConfirmWithIssuedRetentionAsync();
        var sri = new SriBoundaryDouble(_companyId);

        // Job de recuperación (proceso automático) sobre ambas: el Draft de la anterior nace DESPUÉS de habilitar.
        (await StartWithRealSwitchAsync(before, Guid.Empty, sri)).Code
            .Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        (await StartWithRealSwitchAsync(after, Guid.Empty, sri)).IsSuccess.Should().BeTrue();
        sri.SendCalls.Should().Be(1, "solo la posterior");

        var beforeDraft = await DocumentOfAsync(before);
        beforeDraft.CreatedAt.Should().BeAfter(await EnabledSinceAsync(),
            "el Draft es posterior a la habilitación y aun así no se transmite");
        (await RetryCandidateIdsAsync()).Should().NotContain(beforeDraft.Id);
        (await RetryWithRealSwitchAsync(beforeDraft.Id, Guid.Empty, sri)).Code
            .Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        sri.SendCalls.Should().Be(1);

        // Reanudación explícita de un usuario.
        (await StartWithRealSwitchAsync(before, _userId, sri)).IsSuccess.Should().BeTrue();
        sri.SendCalls.Should().Be(2);
    }

    [Fact]
    public async Task SRI_TOGGLE_04_rehabilitacion_entre_la_lectura_del_job_y_el_INSERT_del_Draft_no_transmite()
    {
        await SetEmissionAuditedAsync(true);
        var (_, retentionId) = await ConfirmWithIssuedRetentionAsync();
        (await RecoveryCandidatesAsync()).Should().Contain(c => c.RetentionId == retentionId);

        // Entre la lectura del candidato y el INSERT del Draft: apagado y re-habilitación (corte nuevo).
        await SetEmissionAuditedAsync(false);
        await SetEmissionAuditedAsync(true);

        var sri = new SriBoundaryDouble(_companyId);
        (await StartWithRealSwitchAsync(retentionId, Guid.Empty, sri)).Code
            .Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        var draft = await DocumentOfAsync(retentionId);
        draft.CreatedAt.Should().BeAfter(await EnabledSinceAsync());
        (await RetryCandidateIdsAsync()).Should().NotContain(draft.Id);
        sri.SendCalls.Should().Be(0);
    }

    [Fact]
    public async Task SRI_TOGGLE_07_un_cambio_durante_la_firma_espera_a_la_llamada_en_curso_y_despues_no_hay_llamadas_nuevas()
    {
        await SetEmissionAuditedAsync(true);
        var (_, retentionId) = await ConfirmWithIssuedRetentionAsync();
        var sri = new SriBoundaryDouble(_companyId) { HoldSign = new(TaskCreationOptions.RunContinuationsAsynchronously) };

        // La recuperación automática (emitida después de la habilitación) entra al ámbito SRI; la firma se retiene.
        var recovery = StartWithRealSwitchAsync(retentionId, Guid.Empty, sri);
        await sri.SignEntered.Task.WaitAsync(TimeSpan.FromSeconds(30));

        // SRI-TOGGLE-07 (sustituye la ventana de 04): apagado + re-habilitación no se confirman mientras la llamada
        // siga en curso; la llamada ve un estado estable y termina su único envío.
        var toggle = Task.Run(async () =>
        {
            await SetEmissionAuditedAsync(false);
            await SetEmissionAuditedAsync(true);
        });
        (await Task.WhenAny(toggle, Task.Delay(TimeSpan.FromSeconds(1)))).Should().NotBe(toggle);
        sri.HoldSign.SetResult();

        (await recovery.WaitAsync(TimeSpan.FromSeconds(30))).IsSuccess.Should().BeTrue();
        await toggle.WaitAsync(TimeSpan.FromSeconds(30));
        sri.SendCalls.Should().Be(1, "la llamada iniciada antes del cambio terminó antes de su confirmación");

        // Confirmados los cambios, ningún proceso automático vuelve a llamar por este comprobante.
        var document = await DocumentOfAsync(retentionId);
        (await RetryCandidateIdsAsync()).Should().NotContain(document.Id);
        await RetryWithRealSwitchAsync(document.Id, Guid.Empty, sri);
        sri.SendCalls.Should().Be(1);
    }

    [Fact]
    public async Task SRI_TOGGLE_04_tras_reiniciar_procesos_los_pendientes_no_se_reanudan_solos()
    {
        // Emisión deshabilitada: la confirmación deja el Draft sin transmitir.
        var (_, retentionId) = await ConfirmWithIssuedRetentionAsync();
        var sri = new SriBoundaryDouble(_companyId);
        (await StartWithRealSwitchAsync(retentionId, _userId, sri)).Code
            .Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        await SetEmissionAuditedAsync(true);

        // "Reinicio": contextos, issuer y switch nuevos; el corte y la actividad viven solo en PostgreSQL.
        var draft = await DocumentOfAsync(retentionId);
        (await RetryCandidateIdsAsync()).Should().NotContain(draft.Id);
        (await RetryWithRealSwitchAsync(draft.Id, Guid.Empty, sri)).Code
            .Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        (await StartWithRealSwitchAsync(retentionId, Guid.Empty, sri)).Code
            .Should().Be(ApiResponseCodes.ElectronicDocuments.EmissionDisabled);
        sri.SendCalls.Should().Be(0);

        (await RetryWithRealSwitchAsync(draft.Id, _userId, sri)).IsSuccess.Should().BeTrue();
        sri.SendCalls.Should().Be(1);
    }

    private async Task SetEmissionAuditedAsync(bool enabled)
    {
        await using var db = CreateContext();
        await ElectronicEmissionTestSupport.SetAuditedAsync(db, _tenantId, _companyId, enabled, _userId);
    }

    private async Task<DateTime> EnabledSinceAsync()
    {
        await using var db = CreateContext();
        return (await new ElectronicEmissionSwitch(db).GetAsync(_tenantId, _companyId, false)).EnabledSinceUtc!.Value;
    }

    private async Task<ERP.Domain.Modules.ElectronicDocuments.Entities.ElectronicDocument> DocumentOfAsync(Guid retentionId)
    {
        await using var db = CreateContext();
        return await db.ElectronicDocuments.AsNoTracking()
            .SingleAsync(d => d.SourceModule == RetentionSource && d.SourceEntityId == retentionId);
    }

    private async Task<Result<ElectronicDocumentDto>> StartWithRealSwitchAsync(
        Guid retentionId, Guid userId, SriBoundaryDouble sri)
    {
        await using var db = CreateContext();
        return await RetentionElectronicTestWiring
            .Transmission(db, new FixedCurrentCompany(_companyId), sri, new ElectronicEmissionSwitch(db))
            .StartAsync(_tenantId, _companyId, retentionId, userId);
    }

    private async Task<Result<ElectronicDocumentDto>> RetryWithRealSwitchAsync(
        Guid documentId, Guid userId, SriBoundaryDouble sri)
    {
        await using var db = CreateContext();
        return await RetentionElectronicTestWiring
            .Issuer(db, new FixedCurrentCompany(_companyId), sri, new ElectronicEmissionSwitch(db))
            .RetryAsync(_tenantId, documentId, userId);
    }

    private async Task<IReadOnlyList<Guid>> RetryCandidateIdsAsync()
    {
        await using var db = CreateJobContext();
        return (await new ElectronicDocumentRepository(db, new CompanyClock(db)).GetRetryCandidatesAsync(50))
            .Select(c => c.ElectronicDocumentId).ToList();
    }
}
