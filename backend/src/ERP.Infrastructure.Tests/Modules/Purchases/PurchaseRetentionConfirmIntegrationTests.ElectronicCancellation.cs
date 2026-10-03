using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.ElectronicDocuments;
using ERP.Infrastructure.Persistence.Repositories.Retentions;
using ERP.Infrastructure.Persistence.Services;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ERP.Infrastructure.Tests.Modules.Purchases;

/// <summary>
/// ZH-RETENTION-ELECTRONIC-LIFECYCLE-01A (ADR-036) — ciclo electrónico de la retención de una Compra
/// contra PostgreSQL real: gate SSOT de ciclo de vida, reclamo Dispatching bajo el lock de la
/// retención, anulación del origen según el estado electrónico, transmisión automática post-commit
/// y recuperación idempotente. Solo se simula la frontera SRI (<see cref="SriBoundaryDouble"/>).
///
/// Reemplaza (invertidas) las pruebas de caracterización de ZH-RETENTION-ELECTRONIC-CANCELLATION-ADR-01
/// que documentaban el bug: el XML ya no sale después de anular, el reintento ya no reenvía a ciegas
/// y la anulación con el comprobante en proceso/autorizado ya no es silenciosa.
/// </summary>
public sealed partial class PurchaseRetentionConfirmIntegrationTests
{
    private const string RetentionSource = "Retentions";

    // ── Helpers ───────────────────────────────────────────────────────────────────────────────

    private async Task<(Guid InvoiceId, Guid RetentionId)> ConfirmWithIssuedRetentionAsync(SriBoundaryDouble? sri = null)
    {
        var invoiceId = await SeedDraftPurchaseAsync();
        await using (var db = CreateWiredContext())
        // La transmisión post-commit usa su propio contexto en el test: el fan-out MediatR de
        // CreateWiredContext solo registra lo necesario para compra/retención/contabilidad, y los
        // suscriptores de ElectronicDocumentAuthorizedEvent (p.ej. correo de Ventas) no se resuelven ahí.
        await using (var electronicDb = CreateContext())
        {
            var transmission = sri is null
                ? null
                : RetentionElectronicTestWiring.Transmission(electronicDb, new FixedCurrentCompany(_companyId), sri);
            var confirmed = await ConfirmHandler(db, transmission: transmission)
                .Handle(new ConfirmPurchaseCommand(invoiceId, null, VatIntent()), CancellationToken.None);
            confirmed.IsSuccess.Should().BeTrue(confirmed.Error);
        }
        var retention = (await ReadAsync(invoiceId)).Retentions.Single();
        retention.Status.Should().Be(RetentionStatus.Issued);
        return (invoiceId, retention.Id);
    }

    /// <summary>Siembra un ElectronicDocument "histórico" en el estado pedido, solo vía transiciones de dominio.</summary>
    private async Task<Guid> SeedElectronicDocumentAsync(Guid retentionId, ElectronicDocumentState state)
    {
        await using var db = CreateContext();
        var document = ElectronicDocument.Create(
            _tenantId, _companyId, ElectronicDocumentType.Retention, RetentionSource, retentionId, _userId);
        switch (state)
        {
            case ElectronicDocumentState.Draft:
                break;
            case ElectronicDocumentState.Failed:
                document.MarkFailed("Fallo previo a la firma (test)", _userId);
                break;
            default:
                document.MarkXmlGenerated("retentions/draft.xml", "2.0.0", "2.0.0", _userId);
                document.MarkSigned(SriBoundaryDouble.SignedPath, AccessKey.Create(SriBoundaryDouble.NewAccessKey()), _userId);
                if (state == ElectronicDocumentState.Dispatching)
                    document.MarkDispatching(_userId);
                if (state is ElectronicDocumentState.Sent or ElectronicDocumentState.Received)
                    document.MarkSent(_userId);
                if (state == ElectronicDocumentState.Received)
                    document.MarkReceived(_userId);
                if (state == ElectronicDocumentState.DeadLetter)
                    document.MarkDeadLetter("Reintentos agotados (test)", _userId);
                break;
        }
        db.ElectronicDocuments.Add(document);
        await db.SaveChangesAsync();
        return document.Id;
    }

    private async Task<ElectronicDocument?> ReadElectronicAsync(Guid retentionId)
    {
        await using var db = CreateContext();
        return await db.ElectronicDocuments.AsNoTracking()
            .SingleOrDefaultAsync(d => d.SourceModule == RetentionSource && d.SourceEntityId == retentionId);
    }

    private async Task<int> CountElectronicAsync(Guid retentionId)
    {
        await using var db = CreateContext();
        return await db.ElectronicDocuments.AsNoTracking()
            .CountAsync(d => d.SourceModule == RetentionSource && d.SourceEntityId == retentionId);
    }

    private async Task<Result<ElectronicDocumentDto>> StartTransmissionAsync(Guid retentionId, SriBoundaryDouble sri)
    {
        await using var db = CreateContext();
        return await RetentionElectronicTestWiring
            .Transmission(db, new FixedCurrentCompany(_companyId), sri)
            .StartAsync(_tenantId, _companyId, retentionId, _userId);
    }

    private async Task<Result<ElectronicDocumentDto>> RetryAsync(Guid documentId, SriBoundaryDouble sri)
    {
        await using var db = CreateContext();
        return await RetentionElectronicTestWiring
            .Issuer(db, new FixedCurrentCompany(_companyId), sri)
            .RetryAsync(_tenantId, documentId, _userId);
    }

    private async Task<int> StockMovementCountAsync(Guid invoiceId)
    {
        await using var db = CreateContext();
        return await db.Set<ERP.Domain.Modules.Inventory.Entities.StockMovement>().AsNoTracking()
            .CountAsync(m => m.SourceDocId == invoiceId);
    }

    /// <summary>Compra, retención, CxP y asientos intactos tras una anulación bloqueada.</summary>
    private async Task ShouldBeUntouchedConfirmedAsync(Guid invoiceId, Guid retentionId, int stockMovements)
    {
        var s = await ReadAsync(invoiceId);
        s.Status.Should().Be(PurchaseStatus.Confirmed);
        s.Retentions.Should().ContainSingle().Which.Status.Should().Be(RetentionStatus.Issued);
        var payable = s.Payables.Should().ContainSingle().Which;
        payable.RetainedAmount.Should().Be(4.5m, "la CxP no se revierte");
        payable.Status.Should().NotBe(ERP.Domain.Modules.Payables.Enums.AccountsPayableStatus.Cancelled);
        var accounting = await RetentionAccountingAsync(retentionId);
        accounting.IssuedStatus.Should().NotBe(JournalEntryStatus.Reversed, "el asiento de la retención no se revierte");
        accounting.Reversals.Should().Be(0);
        (await StockMovementCountAsync(invoiceId)).Should().Be(stockMovements, "el Kardex no se revierte");
    }

    // ── 1. Cancel first / Send after → cero llamadas externas ────────────────────────────────

    [Fact]
    public async Task Anulacion_primero_y_transmision_despues_no_genera_XML_ni_llama_al_SRI()
    {
        var (invoiceId, retentionId) = await ConfirmWithIssuedRetentionAsync();
        (await CancelPurchaseAsync(invoiceId)).IsSuccess.Should().BeTrue();

        var sri = new SriBoundaryDouble(_companyId);
        var start = await StartTransmissionAsync(retentionId, sri);

        start.IsSuccess.Should().BeFalse();
        start.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.SourceNotProcessable);
        sri.SignCalls.Should().Be(0);
        sri.SendCalls.Should().Be(0);
        sri.AuthorizationCalls.Should().Be(0);
        (await CountElectronicAsync(retentionId)).Should().Be(0, "nunca se registra un comprobante de una retención anulada");
    }

    [Fact]
    public async Task Anulacion_durante_el_pipeline_antes_del_reclamo_deja_Discarded_y_el_XML_nunca_sale()
    {
        var (invoiceId, retentionId) = await ConfirmWithIssuedRetentionAsync();
        var sri = new SriBoundaryDouble(_companyId)
        {
            HoldSign = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
        };

        // La transmisión ya leyó la retención Issued, registró el Draft y está firmando…
        var startTask = StartTransmissionAsync(retentionId, sri);
        await sri.SignEntered.Task.WaitAsync(TimeSpan.FromSeconds(30));

        // …y entonces se anula la compra: el comprobante nunca tuvo intento externo → Discarded.
        var cancel = await CancelPurchaseAsync(invoiceId).WaitAsync(TimeSpan.FromSeconds(30));
        cancel.IsSuccess.Should().BeTrue(cancel.Error);

        sri.HoldSign.SetResult();
        var start = await startTask.WaitAsync(TimeSpan.FromSeconds(30));

        start.IsSuccess.Should().BeFalse("el reclamo de despacho revalida bajo el lock y la retención ya está anulada");
        sri.SendCalls.Should().Be(0, "el XML jamás sale después de Cancelled");
        sri.AuthorizationCalls.Should().Be(0);
        var electronic = await ReadElectronicAsync(retentionId);
        electronic!.CurrentState.Should().Be(ElectronicDocumentState.Discarded);
        (await ReadAsync(invoiceId)).Retentions.Single().Status.Should().Be(RetentionStatus.Cancelled);
        (await RetentionAccountingAsync(retentionId)).Reversals.Should().Be(1);
    }

    // ── 2. Send claim first / Cancel after → cancelación bloqueada ───────────────────────────

    [Fact]
    public async Task Reclamo_de_envio_primero_bloquea_la_anulacion_sin_reversos()
    {
        var (invoiceId, retentionId) = await ConfirmWithIssuedRetentionAsync();
        var stockMovements = await StockMovementCountAsync(invoiceId);
        var sri = new SriBoundaryDouble(_companyId)
        {
            HoldSend = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
        };

        var startTask = StartTransmissionAsync(retentionId, sri);
        await sri.SendEntered.Task.WaitAsync(TimeSpan.FromSeconds(30));
        (await ReadElectronicAsync(retentionId))!.CurrentState.Should().Be(
            ElectronicDocumentState.Dispatching,
            "el reclamo se persiste ANTES de la llamada externa"
        );

        var cancel = await CancelPurchaseAsync(invoiceId).WaitAsync(TimeSpan.FromSeconds(30));

        cancel.IsSuccess.Should().BeFalse();
        cancel.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.SourceCancellationInProcess);
        await ShouldBeUntouchedConfirmedAsync(invoiceId, retentionId, stockMovements);

        sri.HoldSend.SetResult();
        (await startTask.WaitAsync(TimeSpan.FromSeconds(30))).IsSuccess.Should().BeTrue();
        (await ReadElectronicAsync(retentionId))!.CurrentState.Should().Be(ElectronicDocumentState.Authorized);
        sri.SendCalls.Should().Be(1);
    }

    // ── 3/4. Retención anulada (datos históricos) → reintento y reactivación bloqueados ──────

    [Theory]
    [InlineData(ElectronicDocumentState.Signed)]
    [InlineData(ElectronicDocumentState.Received)]
    [InlineData(ElectronicDocumentState.Failed)]
    public async Task Retencion_anulada_el_reintento_queda_bloqueado_sin_llamadas_ni_escrituras(ElectronicDocumentState state)
    {
        var (invoiceId, retentionId) = await ConfirmWithIssuedRetentionAsync();
        (await CancelPurchaseAsync(invoiceId)).IsSuccess.Should().BeTrue();
        var documentId = await SeedElectronicDocumentAsync(retentionId, state);
        var before = await ReadElectronicAsync(retentionId);

        var sri = new SriBoundaryDouble(_companyId);
        var retry = await RetryAsync(documentId, sri);

        retry.IsSuccess.Should().BeFalse();
        retry.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.SourceNotProcessable);
        sri.SignCalls.Should().Be(0);
        sri.SendCalls.Should().Be(0);
        sri.AuthorizationCalls.Should().Be(0);
        var after = await ReadElectronicAsync(retentionId);
        after!.CurrentState.Should().Be(state);
        after.RetryCount.Should().Be(before!.RetryCount);
    }

    [Fact]
    public async Task Retencion_anulada_la_reactivacion_de_DeadLetter_queda_bloqueada()
    {
        var (invoiceId, retentionId) = await ConfirmWithIssuedRetentionAsync();
        (await CancelPurchaseAsync(invoiceId)).IsSuccess.Should().BeTrue();
        var documentId = await SeedElectronicDocumentAsync(retentionId, ElectronicDocumentState.DeadLetter);

        var sri = new SriBoundaryDouble(_companyId);
        var retry = await RetryAsync(documentId, sri);

        retry.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.SourceNotProcessable);
        sri.SendCalls.Should().Be(0);
        sri.AuthorizationCalls.Should().Be(0);
        var after = await ReadElectronicAsync(retentionId);
        after!.CurrentState.Should().Be(ElectronicDocumentState.DeadLetter, "no se reactiva");
        after.PreDeadLetterState.Should().Be(ElectronicDocumentState.Signed);
    }

    // ── 5/6/7. Estado incierto: nunca reenvío, solo consulta ─────────────────────────────────

    [Fact]
    public async Task Signed_historico_no_se_reenvia_solo_se_consulta_y_sin_respuesta_queda_para_conciliar()
    {
        var (_, retentionId) = await ConfirmWithIssuedRetentionAsync();
        var documentId = await SeedElectronicDocumentAsync(retentionId, ElectronicDocumentState.Signed);
        var sri = new SriBoundaryDouble(_companyId) { AuthorizationStatus = "TIMEOUT" };

        (await RetryAsync(documentId, sri)).IsSuccess.Should().BeTrue();

        sri.SendCalls.Should().Be(0, "un Signed histórico es ambiguo: nunca se reenvía a ciegas");
        sri.AuthorizationCalls.Should().Be(1);
        var electronic = await ReadElectronicAsync(retentionId);
        electronic!.CurrentState.Should().Be(ElectronicDocumentState.Signed);
        electronic.RetryCount.Should().Be(1);
        ElectronicDocumentSourceStatusMapper.From(electronic)
            .Should().Be(ElectronicDocumentSourceStatus.RequiresReconciliation);
    }

    [Fact]
    public async Task Signed_historico_con_respuesta_concluyente_del_SRI_se_resuelve_sin_reenviar()
    {
        var (_, retentionId) = await ConfirmWithIssuedRetentionAsync();
        var documentId = await SeedElectronicDocumentAsync(retentionId, ElectronicDocumentState.Signed);
        var sri = new SriBoundaryDouble(_companyId) { AuthorizationStatus = "AUTORIZADO" };

        (await RetryAsync(documentId, sri)).IsSuccess.Should().BeTrue();

        sri.SendCalls.Should().Be(0);
        (await ReadElectronicAsync(retentionId))!.CurrentState.Should().Be(ElectronicDocumentState.Authorized);
    }

    [Fact]
    public async Task Dispatching_no_se_reenvia_aunque_el_envio_haya_fallado_solo_se_consulta()
    {
        var (_, retentionId) = await ConfirmWithIssuedRetentionAsync();
        var sri = new SriBoundaryDouble(_companyId) { ReceptionTransportFailure = true, AuthorizationStatus = "TIMEOUT" };

        // Envío inicial con fallo de transporte: el SRI pudo haberlo recibido.
        (await StartTransmissionAsync(retentionId, sri)).IsSuccess.Should().BeTrue();
        var electronic = await ReadElectronicAsync(retentionId);
        electronic!.CurrentState.Should().Be(ElectronicDocumentState.Dispatching, "nunca vuelve a Signed");
        sri.SendCalls.Should().Be(1);

        var retry = await RetryAsync(electronic.Id, sri);

        retry.IsSuccess.Should().BeTrue();
        sri.SendCalls.Should().Be(1, "Dispatching jamás se reenvía automáticamente");
        sri.AuthorizationCalls.Should().Be(1);
        electronic = await ReadElectronicAsync(retentionId);
        electronic!.CurrentState.Should().Be(ElectronicDocumentState.Dispatching);
        ElectronicDocumentSourceStatusMapper.From(electronic)
            .Should().Be(ElectronicDocumentSourceStatus.RequiresReconciliation);
    }

    [Fact]
    public async Task Received_solo_consulta_autorizacion()
    {
        var (_, retentionId) = await ConfirmWithIssuedRetentionAsync();
        var documentId = await SeedElectronicDocumentAsync(retentionId, ElectronicDocumentState.Received);
        var sri = new SriBoundaryDouble(_companyId);

        (await RetryAsync(documentId, sri)).IsSuccess.Should().BeTrue();

        sri.SendCalls.Should().Be(0);
        sri.AuthorizationCalls.Should().Be(1);
        (await ReadElectronicAsync(retentionId))!.CurrentState.Should().Be(ElectronicDocumentState.Authorized);
    }

    // ── 8. Authorized → CancelPurchase bloqueado, sin reversos ───────────────────────────────

    [Fact]
    public async Task Autorizada_por_el_SRI_bloquea_la_anulacion_de_la_compra_sin_reversos()
    {
        var sri = new SriBoundaryDouble(_companyId);
        var (invoiceId, retentionId) = await ConfirmWithIssuedRetentionAsync(sri);
        (await ReadElectronicAsync(retentionId))!.CurrentState.Should().Be(ElectronicDocumentState.Authorized);
        var stockMovements = await StockMovementCountAsync(invoiceId);

        var cancel = await CancelPurchaseAsync(invoiceId);

        cancel.IsSuccess.Should().BeFalse();
        cancel.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.SourceCancellationRequiresSriAnnulment);
        cancel.Error.Should().Contain("anulación electrónica");
        await ShouldBeUntouchedConfirmedAsync(invoiceId, retentionId, stockMovements);
        (await ReadElectronicAsync(retentionId))!.CurrentState.Should().Be(ElectronicDocumentState.Authorized);
    }

    [Theory]
    [InlineData(ElectronicDocumentState.Signed)]
    [InlineData(ElectronicDocumentState.Received)]
    [InlineData(ElectronicDocumentState.DeadLetter)]
    public async Task Comprobante_en_estado_incierto_bloquea_la_anulacion_de_la_compra(ElectronicDocumentState state)
    {
        var (invoiceId, retentionId) = await ConfirmWithIssuedRetentionAsync();
        await SeedElectronicDocumentAsync(retentionId, state);
        var stockMovements = await StockMovementCountAsync(invoiceId);

        var cancel = await CancelPurchaseAsync(invoiceId);

        cancel.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.SourceCancellationInProcess);
        await ShouldBeUntouchedConfirmedAsync(invoiceId, retentionId, stockMovements);
    }

    // ── 10. Nunca transmitido → anulación permitida, Discarded, reversos exact-once ───────────

    [Theory]
    [InlineData(ElectronicDocumentState.Draft)]
    [InlineData(ElectronicDocumentState.Failed)]
    public async Task Nunca_transmitido_la_anulacion_se_permite_y_el_comprobante_queda_Discarded(ElectronicDocumentState state)
    {
        var (invoiceId, retentionId) = await ConfirmWithIssuedRetentionAsync();
        await SeedElectronicDocumentAsync(retentionId, state);

        var cancel = await CancelPurchaseAsync(invoiceId);

        cancel.IsSuccess.Should().BeTrue(cancel.Error);
        var s = await ReadAsync(invoiceId);
        s.Status.Should().Be(PurchaseStatus.Cancelled);
        s.Retentions.Single().Status.Should().Be(RetentionStatus.Cancelled);
        s.Payables.Single().RetainedAmount.Should().Be(0m);
        (await RetentionAccountingAsync(retentionId)).Reversals.Should().Be(1);
        var electronic = await ReadElectronicAsync(retentionId);
        electronic!.CurrentState.Should().Be(ElectronicDocumentState.Discarded);
        electronic.LastError.Should().Contain("Anulación automática");
        await using var db = CreateContext();
        var audit = await db.ElectronicDocumentAudits.AsNoTracking()
            .SingleAsync(a => a.EntityId == electronic.Id && a.Action == "Discarded");
        audit.FromState.Should().Be(state);
        audit.ToState.Should().Be(ElectronicDocumentState.Discarded);
        audit.Reason.Should().Contain("Anulación automática", "queda registrado cuándo y por qué se descartó");
    }

    [Fact]
    public async Task Sin_comprobante_la_anulacion_se_permite_sin_crear_ninguno()
    {
        var (invoiceId, retentionId) = await ConfirmWithIssuedRetentionAsync();

        (await CancelPurchaseAsync(invoiceId)).IsSuccess.Should().BeTrue();

        (await CountElectronicAsync(retentionId)).Should().Be(0);
        (await RetentionAccountingAsync(retentionId)).Reversals.Should().Be(1);
    }

    // ── 11. Confirmar compra con retención → transmisión automática post-commit ───────────────

    [Fact]
    public async Task Confirmar_compra_con_retencion_inicia_la_transmision_electronica_automaticamente()
    {
        var sri = new SriBoundaryDouble(_companyId);

        var (_, retentionId) = await ConfirmWithIssuedRetentionAsync(sri);

        var electronic = await ReadElectronicAsync(retentionId);
        electronic.Should().NotBeNull("la transmisión arranca sin acción manual");
        electronic!.DocumentType.Should().Be(ElectronicDocumentType.Retention);
        electronic.CurrentState.Should().Be(ElectronicDocumentState.Authorized);
        sri.SendCalls.Should().Be(1);
    }

    [Fact]
    public async Task Fallo_del_SRI_tras_confirmar_no_revierte_la_compra()
    {
        var sri = new SriBoundaryDouble(_companyId);
        sri.Signing
            .Setup(s => s.SignAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ERP.Application.Modules.ElectronicDocuments.DTOs.ElectronicDocumentXml>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Certificado inválido (test)"));

        var (invoiceId, retentionId) = await ConfirmWithIssuedRetentionAsync(sri);

        (await ReadAsync(invoiceId)).Status.Should().Be(PurchaseStatus.Confirmed);
        (await ReadElectronicAsync(retentionId))!.CurrentState.Should().Be(ElectronicDocumentState.Failed);
        sri.SendCalls.Should().Be(0);
    }

    // ── 13/14. Recuperación idempotente ───────────────────────────────────────────────────────

    /// <summary>Contexto sin tenant ni empresa, como el job Hangfire antes de fijar JobExecutionContext.</summary>
    private ErpDbContext CreateJobContext() =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options,
            new FixedCurrentTenant(Guid.Empty),
            new NoOpPublisher(),
            new FixedCurrentCompany(Guid.Empty)
        );

    private async Task<IReadOnlyList<ERP.Domain.Modules.Retentions.Interfaces.RetentionElectronicStartCandidate>> RecoveryCandidatesAsync()
    {
        await using var db = CreateJobContext();
        return await new RetentionDocumentRepository(db, new FixedCurrentCompany(Guid.Empty))
            .GetPendingElectronicStartAsync(DateTime.UtcNow.AddMinutes(1), 50);
    }

    [Fact]
    public async Task Recuperacion_encuentra_la_retencion_sin_transmitir_y_ejecutada_dos_veces_crea_un_solo_comprobante()
    {
        // Simula una caída entre el commit de la confirmación y el inicio de la transmisión.
        var (_, retentionId) = await ConfirmWithIssuedRetentionAsync();
        var candidates = await RecoveryCandidatesAsync();
        candidates.Should().ContainSingle(c => c.RetentionId == retentionId && c.TenantId == _tenantId && c.CompanyId == _companyId);

        var sri = new SriBoundaryDouble(_companyId);
        var first = await StartTransmissionAsync(retentionId, sri);
        var second = await StartTransmissionAsync(retentionId, sri);

        first.IsSuccess.Should().BeTrue(first.Error);
        second.IsSuccess.Should().BeFalse();
        second.Code.Should().Be(ApiResponseCodes.Common.Conflict);
        (await CountElectronicAsync(retentionId)).Should().Be(1);
        sri.SendCalls.Should().Be(1);
        (await RecoveryCandidatesAsync()).Should().NotContain(c => c.RetentionId == retentionId);
    }

    [Fact]
    public async Task Recuperacion_ignora_las_retenciones_dentro_de_la_ventana_de_gracia()
    {
        var (_, retentionId) = await ConfirmWithIssuedRetentionAsync();
        await using var db = CreateJobContext();

        var candidates = await new RetentionDocumentRepository(db, new FixedCurrentCompany(Guid.Empty))
            .GetPendingElectronicStartAsync(DateTime.UtcNow.AddMinutes(-2), 50);

        candidates.Should().NotContain(c => c.RetentionId == retentionId);
    }

    [Fact]
    public async Task Carrera_recuperacion_y_transmision_inmediata_crea_un_solo_comprobante()
    {
        var (_, retentionId) = await ConfirmWithIssuedRetentionAsync();
        var sri = new SriBoundaryDouble(_companyId);

        var results = await Task.WhenAll(
            StartTransmissionAsync(retentionId, sri),
            StartTransmissionAsync(retentionId, sri)
        );

        results.Count(r => r.IsSuccess).Should().Be(1, string.Join(" | ", results.Select(r => r.Code + ":" + r.Error)));
        (await CountElectronicAsync(retentionId)).Should().Be(1);
        sri.SendCalls.Should().Be(1);
    }

    // ── Job genérico de reintento: candidatos visibles sin contexto de tenant ──────────────────

    [Fact]
    public async Task GetRetryCandidatesAsync_sin_contexto_de_tenant_devuelve_el_candidato_con_su_tenant_y_empresa()
    {
        // ZH-ELECTRONIC-RETRY-TENANT-CONTEXT-01 (antes: hallazgo de ADR-036 §23.3, 0 filas). La consulta
        // es cross-tenant y devuelve solo identificadores; el job procesa cada uno bajo su contexto.
        var (_, retentionId) = await ConfirmWithIssuedRetentionAsync();
        var documentId = await SeedElectronicDocumentAsync(retentionId, ElectronicDocumentState.Failed);

        await using var db = CreateJobContext();
        var candidates = await new ElectronicDocumentRepository(db, new CompanyClock(db)).GetRetryCandidatesAsync();

        var candidate = candidates.Should().ContainSingle(c => c.ElectronicDocumentId == documentId).Subject;
        candidate.TenantId.Should().Be(_tenantId);
        candidate.CompanyId.Should().Be(_companyId);
    }
}
