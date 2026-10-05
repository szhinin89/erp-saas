using System.Text;
using ERP.Application.Common;
using ERP.Application.Modules.Ride.UseCases.GetOrGenerateRide;
using ERP.Domain.Modules.Communications.Constants;
using ERP.Domain.Modules.Communications.Entities;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ERP.Infrastructure.Tests.Communications;

/// <summary>
/// ZH-EDOC-COMMUNICATIONS-01 — comprobantes autorizados → comunicación contra PostgreSQL real (flujo
/// real: transacción de ErpDbContext → handler genérico → contributor → cola → outbox → processor →
/// adjuntos resueltos por su módulo dueño). Factura, nota de crédito y retención; sin correo; template
/// inválido por tipo; evento duplicado; reconciliación (simple, repetida, concurrente); multi-tenant.
/// Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class ElectronicDocumentCommunicationIntegrationTests
    : IClassFixture<CommunicationOutboxDeliveryIntegrationTests.Database>,
        IAsyncLifetime
{
    private readonly CommunicationOutboxDeliveryIntegrationTests.Database _db;
    private readonly ElectronicDocumentCommunicationFlow _flow;

    public ElectronicDocumentCommunicationIntegrationTests(
        CommunicationOutboxDeliveryIntegrationTests.Database db
    )
    {
        _db = db;
        _flow = new ElectronicDocumentCommunicationFlow(db);
    }

    public async Task InitializeAsync()
    {
        await using var ctx = _db.Context();
        await ctx.Database.ExecuteSqlRawAsync(
            "DELETE FROM communication_delivery_attempts; DELETE FROM communication_outbox_attachments; DELETE FROM communication_outbox; DELETE FROM communication_templates; DELETE FROM electronic_documents;"
        );
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── los tres comprobantes: un correo, con XML autorizado y RIDE resueltos al enviar ───

    [Fact]
    public async Task Factura_autorizada_envia_un_correo_con_XML_y_RIDE_generado_fuera_de_la_transaccion()
    {
        var invoice = _flow.Invoice(_db.TenantA, _db.CompanyA, "cliente@example.com");

        var document = await _flow.AuthorizeAsync(
            _db.TenantA,
            _db.CompanyA,
            ElectronicDocumentType.Invoice,
            "Sales",
            invoice.Id
        );

        _flow.RideSender.Verify(
            s => s.Send(It.IsAny<GetOrGenerateRideQuery>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "el RIDE no se genera dentro de la transacción fiscal"
        );
        var row = (await RowsAsync()).Should().ContainSingle().Subject;
        row.Purpose.Should().Be(CommunicationPurposes.SalesInvoiceAuthorized);
        row.Status.Should().Be(CommunicationStatus.Pending);
        row.SourceModule.Should().Be("Sales");
        row.SourceType.Should().Be("SalesInvoice");
        row.SourceId.Should().Be(invoice.Id);
        row.RecipientRole.Should().Be(CommunicationRecipientRole.Customer);
        row.Subject.Should().Be("Factura autorizada 001-001-000000001 - ZH Demo");
        (await AttachmentsAsync(row.Id))
            .Should()
            .OnlyContain(a =>
                a.ReferenceId == document.Id && a.FileStoragePath == null && a.BinaryContent == null
            );

        var sender = new CapturingEmailSender();
        await _flow.RunProcessorAsync(sender);

        var sent = sender.Sent.Should().ContainSingle().Subject;
        sent.ToEmail.Should().Be("cliente@example.com");
        AssertLegalAttachments(sent, document, "001-001-000000001");
        (await RowsAsync()).Single().Status.Should().Be(CommunicationStatus.Sent);
    }

    [Fact]
    public async Task Nota_de_credito_autorizada_envia_un_correo_al_cliente_de_la_factura()
    {
        var invoice = _flow.Invoice(_db.TenantA, _db.CompanyA, "cliente@example.com");
        var salesReturn = _flow.CreditNote(invoice);

        var document = await _flow.AuthorizeAsync(
            _db.TenantA,
            _db.CompanyA,
            ElectronicDocumentType.CreditNote,
            "Sales",
            salesReturn.Id
        );

        var row = (await RowsAsync()).Should().ContainSingle().Subject;
        row.Purpose.Should().Be(CommunicationPurposes.SalesCreditNoteAuthorized);
        row.SourceType.Should().Be("SalesReturn");
        row.SourceId.Should().Be(salesReturn.Id);
        row.RecipientRole.Should().Be(CommunicationRecipientRole.Customer);
        row.TemplateKey.Should().Be(CommunicationPurposes.SalesCreditNoteAuthorized);
        row.TemplateSource.Should().Be(CommunicationTemplateSource.Default);
        row.Subject.Should().Be("Nota de credito autorizada 001-001-000000009 - ZH Demo");
        row.BodyText.Should()
            .Contain("Factura modificada: 001-001-000000001")
            .And.Contain("Total: USD 40.00");

        var sender = new CapturingEmailSender();
        await _flow.RunProcessorAsync(sender);

        var sent = sender.Sent.Should().ContainSingle().Subject;
        sent.ToEmail.Should().Be("cliente@example.com");
        AssertLegalAttachments(sent, document, "001-001-000000009");
    }

    [Fact]
    public async Task Retencion_autorizada_envia_un_correo_al_sujeto_retenido()
    {
        var retention = _flow.Retention(_db.TenantA, _db.CompanyA, "proveedor@example.com");

        var document = await _flow.AuthorizeAsync(
            _db.TenantA,
            _db.CompanyA,
            ElectronicDocumentType.Retention,
            "Retentions",
            retention.Id
        );

        var row = (await RowsAsync()).Should().ContainSingle().Subject;
        row.Purpose.Should().Be(CommunicationPurposes.RetentionAuthorized);
        row.SourceModule.Should().Be("Retentions");
        row.SourceType.Should().Be("RetentionDocument");
        row.SourceId.Should().Be(retention.Id);
        row.RecipientRole.Should().Be(CommunicationRecipientRole.Supplier);
        row.Subject.Should().Be("Comprobante de retencion autorizado 001-001-000000007 - ZH Demo");

        var sender = new CapturingEmailSender();
        await _flow.RunProcessorAsync(sender);

        var sent = sender.Sent.Should().ContainSingle().Subject;
        sent.ToEmail.Should().Be("proveedor@example.com");
        AssertLegalAttachments(sent, document, "001-001-000000007");
    }

    // ── sin correo: evidencia durable, nunca SMTP, nunca duplicada ────────────────────────

    [Theory]
    [InlineData(ElectronicDocumentType.Invoice)]
    [InlineData(ElectronicDocumentType.CreditNote)]
    [InlineData(ElectronicDocumentType.Retention)]
    public async Task Sin_correo_queda_Failed_RECIPIENT_MISSING_sin_envio_y_la_autorizacion_se_conserva(
        ElectronicDocumentType type
    )
    {
        var document = await AuthorizeAsync(type, email: null);

        (await DocumentAsync(document.Id))
            .CurrentState.Should()
            .Be(ElectronicDocumentState.Authorized);
        var row = (await RowsAsync()).Should().ContainSingle().Subject;
        row.Status.Should().Be(CommunicationStatus.Failed);
        row.FailureCategory.Should().Be(CommunicationFailureCategory.Permanent);
        row.LastError.Should().StartWith(ApiResponseCodes.Communications.RecipientMissing);
        row.RecipientEmail.Should().BeNull();
        row.Subject.Should().BeNull();
        row.TemplatePayloadJson.Should().NotBeNull();

        var sender = new CapturingEmailSender();
        await _flow.RunProcessorAsync(sender);
        sender.Sent.Should().BeEmpty();

        await _flow.RepublishAsync(document);
        (await _flow.Reconciler().ReconcileAsync())
            .Examined.Should()
            .Be(0, "una fila Failed es evidencia: no se reencola");
        (await RowsAsync()).Should().ContainSingle();
    }

    // ── template inválido por contributor: semántica de la fase 4 ─────────────────────────

    [Theory]
    [InlineData(ElectronicDocumentType.Invoice, CommunicationPurposes.SalesInvoiceAuthorized)]
    [InlineData(ElectronicDocumentType.CreditNote, CommunicationPurposes.SalesCreditNoteAuthorized)]
    [InlineData(ElectronicDocumentType.Retention, CommunicationPurposes.RetentionAuthorized)]
    public async Task Override_invalido_de_cada_tipo_deja_Failed_Configuration_sin_contenido_ni_envio(
        ElectronicDocumentType type,
        string purpose
    )
    {
        await using (var ctx = _db.Context())
        {
            ctx.CommunicationTemplates.Add(
                CommunicationTemplate.Create(
                    _db.TenantA,
                    _db.CompanyA,
                    null,
                    purpose,
                    "Personalizada",
                    CommunicationChannel.Email,
                    "Asunto {{NoDeclarada}}",
                    "<p>x</p>",
                    null,
                    "es",
                    Guid.Empty
                )
            );
            await ctx.SaveChangesAsync();
        }

        var document = await AuthorizeAsync(type, email: "destino@example.com");

        (await DocumentAsync(document.Id))
            .CurrentState.Should()
            .Be(ElectronicDocumentState.Authorized);
        var row = (await RowsAsync()).Should().ContainSingle().Subject;
        row.Purpose.Should().Be(purpose);
        row.Status.Should().Be(CommunicationStatus.Failed);
        row.FailureCategory.Should().Be(CommunicationFailureCategory.Configuration);
        row.LastError.Should().StartWith(ApiResponseCodes.Communications.TemplateInvalid);
        row.Subject.Should().BeNull();
        row.RecipientEmail.Should().Be("destino@example.com");

        var sender = new CapturingEmailSender();
        await _flow.RunProcessorAsync(sender);
        sender.Sent.Should().BeEmpty();
    }

    // ── idempotencia: evento duplicado, reconciliación, concurrencia ──────────────────────

    [Fact]
    public async Task Evento_duplicado_no_duplica_la_comunicacion()
    {
        var document = await AuthorizeAsync(ElectronicDocumentType.Invoice, "cliente@example.com");

        await _flow.RepublishAsync(document);
        await _flow.RepublishAsync(document);

        (await RowsAsync()).Should().ContainSingle();
        var sender = new CapturingEmailSender();
        await _flow.RunProcessorAsync(sender);
        sender.Sent.Should().ContainSingle();
    }

    [Fact]
    public async Task Reconciliacion_encola_lo_que_el_evento_no_encolo_una_sola_vez()
    {
        var document = await AuthorizeAsync(
            ElectronicDocumentType.CreditNote,
            "cliente@example.com",
            publish: false
        );
        (await RowsAsync()).Should().BeEmpty("simula que el evento se perdió");

        var first = await _flow.Reconciler().ReconcileAsync();
        var second = await _flow.Reconciler().ReconcileAsync();

        first
            .Should()
            .Be(
                new ERP.Infrastructure.Communications.ElectronicDocumentCommunicationReconciliationSummary(
                    1,
                    1,
                    0,
                    0,
                    0
                )
            );
        second.Examined.Should().Be(0, "ya tiene comunicación");
        var row = (await RowsAsync()).Should().ContainSingle().Subject;
        row.Purpose.Should().Be(CommunicationPurposes.SalesCreditNoteAuthorized);
        row.SourceId.Should().Be(document.SourceEntityId);

        await _flow.RepublishAsync(document);
        (await RowsAsync())
            .Should()
            .ContainSingle("evento tardío + reconciliación = misma identidad");
    }

    [Fact]
    public async Task Reconciliacion_concurrente_con_eventos_duplicados_produce_una_sola_fila_por_comprobante()
    {
        var documents = new List<ElectronicDocument>();
        foreach (
            var type in new[]
            {
                ElectronicDocumentType.Invoice,
                ElectronicDocumentType.CreditNote,
                ElectronicDocumentType.Retention,
            }
        )
            documents.Add(await AuthorizeAsync(type, "destino@example.com", publish: false));

        var runs = Enumerable
            .Range(0, 6)
            .Select(_ => Task.Run(() => _flow.Reconciler().ReconcileAsync()))
            .Concat<Task>(
                documents.SelectMany(d =>
                    Enumerable.Range(0, 3).Select(_ => Task.Run(() => _flow.RepublishAsync(d)))
                )
            )
            .ToList();
        await Task.WhenAll(runs);

        var rows = await RowsAsync();
        rows.Should().HaveCount(3);
        rows.Select(r => r.IdempotencyKey).Should().OnlyHaveUniqueItems();
        rows.Select(r => r.SourceId)
            .Should()
            .BeEquivalentTo(documents.Select(d => d.SourceEntityId));

        var sender = new CapturingEmailSender();
        await _flow.RunProcessorAsync(sender);
        sender.Sent.Should().HaveCount(3);
    }

    [Fact]
    public async Task Reconciliacion_respeta_la_preferencia_y_la_antiguedad_minima()
    {
        await AuthorizeAsync(ElectronicDocumentType.Invoice, "cliente@example.com", publish: false);

        (await _flow.Reconciler(clockOffset: TimeSpan.Zero).ReconcileAsync())
            .Examined.Should()
            .Be(0, "recién autorizado: el evento aún puede estar en curso");

        _flow.DisableEmailOnAuthorization(_db.CompanyA);
        (await _flow.Reconciler().ReconcileAsync()).Examined.Should().Be(0);
        (await RowsAsync()).Should().BeEmpty();
    }

    // ── horizonte (verificación final): sin límite de antigüedad ──────────────────────────

    [Fact]
    public async Task Comprobante_autorizado_hace_mas_de_7_dias_sin_comunicacion_se_reconcilia_una_sola_vez()
    {
        // 1-3. Authorized hace 30 días, política habilitada (default del flujo), sin CommunicationOutbox.
        var document = await AuthorizeAsync(
            ElectronicDocumentType.Invoice,
            "cliente@example.com",
            publish: false
        );
        await _flow.BackdateAsync(document, TimeSpan.FromDays(30));
        (await RowsAsync()).Should().BeEmpty();

        // 4-5. El reconciliador crea exactamente una intención.
        var first = await _flow.Reconciler().ReconcileAsync();

        first.Queued.Should().Be(1);
        var row = (await RowsAsync()).Should().ContainSingle().Subject;
        row.SourceId.Should().Be(document.SourceEntityId);
        row.Purpose.Should().Be(CommunicationPurposes.SalesInvoiceAuthorized);
        row.Status.Should().Be(CommunicationStatus.Pending);

        // Otra vez: sigue existiendo una sola.
        var second = await _flow.Reconciler().ReconcileAsync();

        second.Examined.Should().Be(0, "ya tiene comunicación");
        (await RowsAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task Faltantes_se_procesan_del_mas_antiguo_al_mas_nuevo()
    {
        var reciente = await AuthorizeAsync(
            ElectronicDocumentType.Invoice,
            "a@example.com",
            publish: false
        );
        var antiguo = await AuthorizeAsync(
            ElectronicDocumentType.Retention,
            "b@example.com",
            publish: false
        );
        await _flow.BackdateAsync(reciente, TimeSpan.FromDays(10));
        await _flow.BackdateAsync(antiguo, TimeSpan.FromDays(400));

        await _flow.Reconciler(maxPerRun: 1).ReconcileAsync();
        (await RowsAsync()).Single().SourceId.Should().Be(antiguo.SourceEntityId);

        await _flow.Reconciler(maxPerRun: 1).ReconcileAsync();
        (await RowsAsync())
            .Select(r => r.SourceId)
            .Should()
            .BeEquivalentTo(new[] { antiguo.SourceEntityId, reciente.SourceEntityId });
    }

    [Fact]
    public async Task Faltantes_no_elegibles_antiguos_no_bloquean_a_los_siguientes()
    {
        // Tres comprobantes viejos cuyo origen no existe (el servicio los omite y siguen faltando) delante
        // de uno elegible más nuevo, con un lote de 2: sin cursor, cada corrida reexaminaría los mismos dos.
        var anomalos = new List<ElectronicDocument>();
        for (var i = 0; i < 3; i++)
        {
            var huerfano = await _flow.AuthorizeAsync(
                _db.TenantA,
                _db.CompanyA,
                ElectronicDocumentType.Invoice,
                "Sales",
                Guid.NewGuid(),
                publish: false
            );
            await _flow.BackdateAsync(huerfano, TimeSpan.FromDays(100 + i));
            anomalos.Add(huerfano);
        }
        var elegible = await AuthorizeAsync(
            ElectronicDocumentType.Invoice,
            "cliente@example.com",
            publish: false
        );
        await _flow.BackdateAsync(elegible, TimeSpan.FromDays(50));

        var run1 = await _flow.Reconciler(maxPerRun: 2).ReconcileAsync();
        run1.Should()
            .Match<ERP.Infrastructure.Communications.ElectronicDocumentCommunicationReconciliationSummary>(
                s => s.Examined == 2 && s.Skipped == 2 && !s.Wrapped
            );
        (await RowsAsync()).Should().BeEmpty();

        var run2 = await _flow.Reconciler(maxPerRun: 2).ReconcileAsync();
        run2.Should()
            .Match<ERP.Infrastructure.Communications.ElectronicDocumentCommunicationReconciliationSummary>(
                s => s.Examined == 2 && s.Queued == 1
            );
        (await RowsAsync()).Should().ContainSingle(r => r.SourceId == elegible.SourceEntityId);

        // Al llegar al final vuelve al más antiguo: los anómalos se reexaminan (por si su origen se corrige).
        run2.Wrapped.Should().BeTrue();
        (await _flow.Reconciler(maxPerRun: 2).ReconcileAsync()).Skipped.Should().Be(2);
        (await RowsAsync()).Should().ContainSingle();
    }

    // ── multi-tenant ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cada_comprobante_se_comunica_en_su_tenant_y_empresa_sin_fuga()
    {
        var invoiceA = _flow.Invoice(_db.TenantA, _db.CompanyA, "a@example.com");
        var invoiceB = _flow.Invoice(_db.TenantB, _db.CompanyB, "b@example.com");
        await _flow.AuthorizeAsync(
            _db.TenantA,
            _db.CompanyA,
            ElectronicDocumentType.Invoice,
            "Sales",
            invoiceA.Id
        );
        await _flow.AuthorizeAsync(
            _db.TenantB,
            _db.CompanyB,
            ElectronicDocumentType.Invoice,
            "Sales",
            invoiceB.Id,
            publish: false
        );

        var summary = await _flow.Reconciler().ReconcileAsync();

        summary
            .Queued.Should()
            .Be(1, "solo B faltaba; la reconciliación cruza tenants con el patrón autorizado");
        var rows = await RowsAsync();
        rows.Should().HaveCount(2);
        rows.Single(r => r.SourceId == invoiceA.Id)
            .Should()
            .Match<CommunicationOutbox>(r =>
                r.TenantId == _db.TenantA
                && r.CompanyId == _db.CompanyA
                && r.RecipientEmail == "a@example.com"
            );
        rows.Single(r => r.SourceId == invoiceB.Id)
            .Should()
            .Match<CommunicationOutbox>(r =>
                r.TenantId == _db.TenantB
                && r.CompanyId == _db.CompanyB
                && r.RecipientEmail == "b@example.com"
            );

        // Un comprobante de A con un origen que solo existe en B no se resuelve: el contributor busca en el tenant del comprobante.
        await _flow.AuthorizeAsync(
            _db.TenantA,
            _db.CompanyA,
            ElectronicDocumentType.Invoice,
            "Sales",
            invoiceB.Id
        );
        (await RowsAsync()).Should().HaveCount(2);

        var sender = new CapturingEmailSender();
        await _flow.RunProcessorAsync(sender);
        sender
            .Sent.Select(m => m.ToEmail)
            .Should()
            .BeEquivalentTo("a@example.com", "b@example.com");
    }

    // ── helpers ───────────────────────────────────────────────────────────────────────────

    private async Task<ElectronicDocument> AuthorizeAsync(
        ElectronicDocumentType type,
        string? email,
        bool publish = true
    )
    {
        var (module, sourceId) = type switch
        {
            ElectronicDocumentType.Invoice => (
                "Sales",
                _flow.Invoice(_db.TenantA, _db.CompanyA, email).Id
            ),
            ElectronicDocumentType.CreditNote => (
                "Sales",
                _flow.CreditNote(_flow.Invoice(_db.TenantA, _db.CompanyA, email)).Id
            ),
            _ => ("Retentions", _flow.Retention(_db.TenantA, _db.CompanyA, email).Id),
        };
        return await _flow.AuthorizeAsync(
            _db.TenantA,
            _db.CompanyA,
            type,
            module,
            sourceId,
            publish
        );
    }

    private void AssertLegalAttachments(
        ERP.Application.Modules.Communications.Services.EmailMessage sent,
        ElectronicDocument document,
        string number
    )
    {
        sent.Attachments.Should().HaveCount(2);
        var xml = sent.Attachments.Single(a => a.ContentType == "application/xml");
        xml.FileName.Should().Be($"{number}-autorizado.xml");
        xml.Content.Should()
            .Equal(
                _flow.Storage[document.AuthorizedXmlPath!],
                "el XML autorizado lo entrega ElectronicDocuments desde su almacenamiento"
            );
        var ride = sent.Attachments.Single(a => a.ContentType == "application/pdf");
        ride.FileName.Should().Be($"{number}-RIDE.pdf");
        Encoding.ASCII.GetString(ride.Content).Should().Be($"%PDF-{document.SourceEntityId}");
    }

    private async Task<List<CommunicationOutbox>> RowsAsync()
    {
        await using var ctx = _db.Context();
        return await ctx.CommunicationOutbox.IgnoreQueryFilters().AsNoTracking().ToListAsync();
    }

    private async Task<List<CommunicationOutboxAttachment>> AttachmentsAsync(Guid communicationId)
    {
        await using var ctx = _db.Context();
        return await ctx.Set<CommunicationOutboxAttachment>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(a => a.CommunicationOutboxId == communicationId)
            .ToListAsync();
    }

    private async Task<ElectronicDocument> DocumentAsync(Guid id)
    {
        await using var ctx = _db.Context();
        return await ctx
            .ElectronicDocuments.IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(d => d.Id == id);
    }
}
