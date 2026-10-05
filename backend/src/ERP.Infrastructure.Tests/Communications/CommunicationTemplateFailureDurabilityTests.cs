using ERP.Application.Common;
using ERP.Application.Modules.Communications.ElectronicDocuments;
using ERP.Application.Modules.Communications.Services;
using ERP.Domain.Modules.Communications.Constants;
using ERP.Domain.Modules.Communications.Entities;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Infrastructure.Persistence.Configurations.Communications;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ERP.Infrastructure.Tests.Communications;

/// <summary>
/// ZH-COMMUNICATIONS-TEMPLATES-01 (verificación final) — durabilidad ante fallo de template por el flujo
/// REAL: el ElectronicDocument autorizado se guarda con ErpDbContext, que publica
/// ElectronicDocumentAuthorizedEvent DENTRO de su transacción al handler genérico real (ZH-EDOC-COMMUNICATIONS-01) → cola real →
/// resolver real (override en PostgreSQL) → renderer → CommunicationOutbox. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class CommunicationTemplateFailureDurabilityTests
    : IClassFixture<CommunicationOutboxDeliveryIntegrationTests.Database>,
        IAsyncLifetime
{
    private readonly CommunicationOutboxDeliveryIntegrationTests.Database _db;
    private readonly ElectronicDocumentCommunicationFlow _flow;

    public CommunicationTemplateFailureDurabilityTests(CommunicationOutboxDeliveryIntegrationTests.Database db)
    {
        _db = db;
        _flow = new ElectronicDocumentCommunicationFlow(db);
    }

    public async Task InitializeAsync()
    {
        await using var ctx = _db.Context();
        await ctx.Database.ExecuteSqlRawAsync(
            "DELETE FROM communication_outbox_attachments; DELETE FROM communication_outbox; DELETE FROM communication_templates; DELETE FROM electronic_documents;"
        );
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Override_invalido_no_revierte_la_autorizacion_y_deja_evidencia_durable_recuperable()
    {
        await AddOverrideAsync(subject: "Factura {{InvoiceNumber}} {{NoDeclarada}}");
        var invoice = AuthorizedInvoice();

        var document = await AuthorizeInTransactionAsync(invoice);

        // 1. La autorización fiscal quedó confirmada.
        (await DocumentAsync(document.Id)).CurrentState.Should().Be(ElectronicDocumentState.Authorized);

        // 2-3. Evidencia durable en la MISMA outbox: Failed por configuración, sin contenido inventado,
        // con identidad, destinatario, origen y las variables para re-renderizar tras corregir el template.
        var row = (await RowsAsync()).Should().ContainSingle().Subject;
        row.Status.Should().Be(CommunicationStatus.Failed);
        row.FailureCategory.Should().Be(CommunicationFailureCategory.Configuration);
        row.LastError.Should().StartWith(ApiResponseCodes.Communications.TemplateInvalid);
        row.Subject.Should().BeNull();
        row.BodyHtml.Should().BeNull();
        row.BodyText.Should().BeNull();
        row.TemplateKey.Should().Be(CommunicationPurposes.SalesInvoiceAuthorized);
        row.TemplateVersion.Should().BeNull();
        row.RecipientEmail.Should().Be("cliente@example.com");
        row.SourceModule.Should().Be("Sales");
        row.SourceId.Should().Be(invoice.Id);
        row.TemplatePayloadJson.Should().Contain("001-001-000000001").And.Contain("\"Total\":\"100.00\"");
        row.IdempotencyKey.Should().NotBeNullOrWhiteSpace();

        // No se envía SMTP.
        var sender = new CountingSender();
        await RunProcessorAsync(sender);
        sender.Calls.Should().Be(0);
        (await RowsAsync()).Single().Status.Should().Be(CommunicationStatus.Failed);

        // Repetir el hecho (re-entrega / reconciliación futura) no duplica.
        var repeated = await RepublishAsync(document);
        repeated.Should().BeTrue("la misma identidad vuelve a la fila existente");
        (await RowsAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task Una_fila_sin_contenido_nunca_puede_quedar_enviable()
    {
        await AddOverrideAsync(subject: "Factura {{InvoiceNumber}} {{NoDeclarada}}");
        await AuthorizeInTransactionAsync(AuthorizedInvoice());
        var id = (await RowsAsync()).Single().Id;

        await using var ctx = _db.Context();
        var act = () => ctx.Database.ExecuteSqlInterpolatedAsync($"UPDATE communication_outbox SET status = 'Pending' WHERE id = {id}");

        (await act.Should().ThrowAsync<PostgresException>())
            .Which.ConstraintName.Should().Be(CommunicationOutboxConfiguration.ContentCheckConstraint);
    }

    [Fact]
    public async Task Default_valido_mantiene_el_flujo_actual()
    {
        var invoice = AuthorizedInvoice();

        var document = await AuthorizeInTransactionAsync(invoice);

        (await DocumentAsync(document.Id)).CurrentState.Should().Be(ElectronicDocumentState.Authorized);
        var row = (await RowsAsync()).Single();
        row.Status.Should().Be(CommunicationStatus.Pending);
        row.TemplateSource.Should().Be(CommunicationTemplateSource.Default);
        row.TemplateVersion.Should().Be(1);
        row.Subject.Should().Be("Factura autorizada 001-001-000000001 - ZH Demo");
        row.TemplatePayloadJson.Should().BeNull("el contenido renderizado ya está en la fila");
        row.FailureCategory.Should().BeNull();

        var sender = new CountingSender();
        await RunProcessorAsync(sender);
        sender.Calls.Should().Be(1);
        (await RowsAsync()).Single().Status.Should().Be(CommunicationStatus.Sent);
    }

    // ── flujo real (ElectronicDocumentCommunicationFlow) ──────────────────────────────────

    private async Task<ElectronicDocument> AuthorizeInTransactionAsync(SalesInvoice invoice) =>
        await _flow.AuthorizeAsync(_db.TenantA, _db.CompanyA, ElectronicDocumentType.Invoice, "Sales", invoice.Id);

    private async Task<bool> RepublishAsync(ElectronicDocument document)
    {
        await using var ctx = _db.Context();
        using var _ = JobExecutionContext.Begin(_db.TenantA, _db.CompanyA);
        var result = await _flow.Service(ctx).RequestAsync(document, ElectronicDocumentCommunicationTrigger.Reconciliation);
        return result.Outcome == ElectronicDocumentCommunicationOutcome.AlreadyQueued;
    }

    private Task RunProcessorAsync(IEmailSender sender) => _flow.RunProcessorAsync(sender);

    private async Task AddOverrideAsync(string subject)
    {
        await using var ctx = _db.Context();
        ctx.CommunicationTemplates.Add(CommunicationTemplate.Create(
            _db.TenantA, _db.CompanyA, null, CommunicationPurposes.SalesInvoiceAuthorized, "Factura personalizada",
            CommunicationChannel.Email, subject, "<p>{{CustomerName}}</p>", null, "es", Guid.Empty));
        await ctx.SaveChangesAsync();
    }

    private SalesInvoice AuthorizedInvoice() => _flow.Invoice(_db.TenantA, _db.CompanyA, "cliente@example.com");

    private async Task<ElectronicDocument> DocumentAsync(Guid id)
    {
        await using var ctx = _db.Context();
        return await ctx.ElectronicDocuments.IgnoreQueryFilters().AsNoTracking().SingleAsync(d => d.Id == id);
    }

    private async Task<List<CommunicationOutbox>> RowsAsync()
    {
        await using var ctx = _db.Context();
        return await ctx.CommunicationOutbox.IgnoreQueryFilters().AsNoTracking().ToListAsync();
    }

    private sealed class CountingSender : IEmailSender
    {
        public int Calls { get; private set; }

        public Task<EmailDeliveryReceipt> SendAsync(EmailMessage message, CommunicationEmailSettings settings, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(EmailDeliveryReceipt.WithoutProviderId);
        }
    }
}
