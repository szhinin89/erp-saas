using ERP.Application.Common;
using ERP.Application.Modules.Communications.DTOs;
using ERP.Application.Modules.Communications.Services;
using ERP.Application.Modules.Communications.Templates;
using ERP.Domain.Modules.Communications.Constants;
using ERP.Domain.Modules.Communications.Entities;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Communications.ValueObjects;
using ERP.Infrastructure.Communications;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Communications;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Infrastructure.Tests.Communications;

/// <summary>
/// ZH-COMMUNICATIONS-TEMPLATES-01 — templates contra PostgreSQL real: override por empresa aislado,
/// override inactivo/inválido, System sin overrides, render al encolar con metadata persistida y
/// mensajes ya encolados inmunes a cambios posteriores del template. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class CommunicationTemplateIntegrationTests
    : IClassFixture<CommunicationOutboxDeliveryIntegrationTests.Database>,
        IAsyncLifetime
{
    private const string OverrideSubject = "Su factura {{InvoiceNumber}} de {{IssuerName}}";
    private readonly CommunicationOutboxDeliveryIntegrationTests.Database _db;

    public CommunicationTemplateIntegrationTests(CommunicationOutboxDeliveryIntegrationTests.Database db) => _db = db;

    public async Task InitializeAsync()
    {
        await using var ctx = _db.Context();
        await ctx.Database.ExecuteSqlRawAsync(
            "DELETE FROM communication_outbox_attachments; DELETE FROM communication_outbox; DELETE FROM communication_templates;"
        );
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Empresa_con_override_lo_usa_y_la_otra_usa_el_default_sin_contaminacion()
    {
        await AddOverrideAsync(_db.TenantA, _db.CompanyA);

        var a = await EnqueueAsync(_db.TenantA, _db.CompanyA);
        var b = await EnqueueAsync(_db.TenantB, _db.CompanyB);

        var rowA = await RowAsync(a.Id);
        rowA.TemplateSource.Should().Be(CommunicationTemplateSource.CompanyOverride);
        rowA.TemplateVersion.Should().Be(1);
        rowA.Subject.Should().Be("Su factura 001-001-000000001 de ZH Demo");

        var rowB = await RowAsync(b.Id);
        rowB.TemplateKey.Should().Be(CommunicationPurposes.SalesInvoiceAuthorized);
        rowB.TemplateSource.Should().Be(CommunicationTemplateSource.Default);
        rowB.TemplateVersion.Should().Be(1);
        rowB.Subject.Should().Be("Factura autorizada 001-001-000000001 - ZH Demo");
        rowB.BodyHtml.Should().Contain("<li><strong>Total:</strong> USD 100.00</li>");

        // El override de A no es visible desde el contexto de B (ni pidiéndolo con los ids de A).
        await using var ctx = _db.Context();
        using (JobExecutionContext.Begin(_db.TenantB, _db.CompanyB))
            (await new CommunicationTemplateRepository(ctx).GetActiveAsync(_db.TenantA, _db.CompanyA, CommunicationChannel.Email, CommunicationPurposes.SalesInvoiceAuthorized, "es"))
                .Should().BeNull();
    }

    [Fact]
    public async Task Override_inactivo_usa_el_default()
    {
        await AddOverrideAsync(_db.TenantA, _db.CompanyA, active: false);

        var queued = await EnqueueAsync(_db.TenantA, _db.CompanyA);

        (await RowAsync(queued.Id)).TemplateSource.Should().Be(CommunicationTemplateSource.Default);
    }

    [Fact]
    public async Task Override_invalido_no_envia_contenido_corrupto_y_queda_registrado_como_Failed()
    {
        await AddOverrideAsync(_db.TenantA, _db.CompanyA, subject: "Factura {{InvoiceNumber}} {{NoDeclarada}}");

        var result = await EnqueueAsync(_db.TenantA, _db.CompanyA);

        result.TemplateFailureCode.Should().Be(ApiResponseCodes.Communications.TemplateInvalid);
        var row = await RowAsync(result.Id);
        row.Status.Should().Be(CommunicationStatus.Failed, "nunca se envía un template inválido ni se reemplaza en silencio por el default");
        row.FailureCategory.Should().Be(CommunicationFailureCategory.Configuration);
        row.Subject.Should().BeNull();
        row.TemplateSource.Should().BeNull();
    }

    [Fact]
    public async Task System_no_consulta_overrides_de_empresa_aunque_existan()
    {
        await AddOverrideAsync(_db.TenantA, _db.CompanyA);
        await using var ctx = _db.Context();

        using (JobExecutionContext.Begin(_db.TenantA, _db.CompanyA))
        {
            var result = await new CommunicationTemplateResolver(new CommunicationTemplateRepository(ctx))
                .ResolveAsync(CommunicationScope.System, CommunicationPurposes.SalesInvoiceAuthorized);

            result.Value!.Source.Should().Be(CommunicationTemplateSource.Default);
        }
    }

    [Fact]
    public async Task Cambiar_el_override_despues_de_encolar_no_cambia_lo_que_se_envia_ni_duplica()
    {
        var overrideId = await AddOverrideAsync(_db.TenantA, _db.CompanyA);
        var queued = await EnqueueAsync(_db.TenantA, _db.CompanyA);
        var original = await RowAsync(queued.Id);

        await using (var ctx = _db.Context())
        {
            var template = await ctx.CommunicationTemplates.IgnoreQueryFilters().SingleAsync(t => t.Id == overrideId);
            template.UpdateContent("Factura", "CAMBIADO {{InvoiceNumber}}", "<p>CAMBIADO</p>", null, Guid.Empty);
            await ctx.SaveChangesAsync();
        }

        var again = await EnqueueAsync(_db.TenantA, _db.CompanyA);
        again.WasAlreadyQueued.Should().BeTrue("la versión del template no forma parte de la identidad");
        again.Id.Should().Be(queued.Id);

        string? sentSubject = null;
        string? sentHtml = null;
        await using (var ctx = _db.Context())
        {
            await new CommunicationOutboxProcessor(
                new CommunicationOutboxDeliveryStore(ctx),
                new CapturingSender((m, _) => { sentSubject = m.Subject; sentHtml = m.BodyHtml; }),
                new InstanceResolver(),
                TimeProvider.System,
                NullLogger<CommunicationOutboxProcessor>.Instance
            ).ProcessPendingAsync();
        }

        sentSubject.Should().Be(original.Subject, "se envía lo renderizado al encolar, no se re-renderiza");
        sentHtml.Should().Be(original.BodyHtml);
        var after = await RowAsync(queued.Id);
        after.Status.Should().Be(CommunicationStatus.Sent);
        after.TemplateVersion.Should().Be(1);
    }

    // ── helpers ───────────────────────────────────────────────────────────────────────────

    private async Task<Guid> AddOverrideAsync(Guid tenantId, Guid companyId, bool active = true, string subject = OverrideSubject)
    {
        await using var ctx = _db.Context();
        var template = CommunicationTemplate.Create(
            tenantId, companyId, null, CommunicationPurposes.SalesInvoiceAuthorized, "Factura personalizada",
            CommunicationChannel.Email, subject, "<p>Hola {{CustomerName}}</p>", "Hola {{CustomerName}}", "es", Guid.Empty
        );
        if (!active)
            template.Deactivate(Guid.Empty);
        ctx.CommunicationTemplates.Add(template);
        await ctx.SaveChangesAsync();
        return template.Id;
    }

    /// <summary>Encola con el contexto de la empresa (como el handler: request de la empresa del documento).</summary>
    private async Task<QueuedCommunicationDto> EnqueueAsync(Guid tenantId, Guid companyId)
    {
        await using var ctx = _db.Context();
        using var _ = JobExecutionContext.Begin(tenantId, companyId);
        var queue = new CommunicationQueue(
            new CommunicationOutboxRepository(ctx),
            new CurrentCompanyService(new HttpContextAccessor()),
            Mock.Of<ICurrentUser>(u => u.UserId == Guid.Empty),
            new InstanceResolver(),
            new CommunicationTemplateResolver(new CommunicationTemplateRepository(ctx)),
            NullLogger<CommunicationQueue>.Instance
        );
        return await queue.EnqueueAsync(
            new CommunicationRequest(
                CommunicationScope.Company(tenantId, companyId),
                CommunicationPurposes.SalesInvoiceAuthorized,
                new CommunicationSource("Sales", "SalesInvoice", companyId),
                CommunicationRecipientRole.Customer,
                "Cliente",
                "cliente@test.com",
                CommunicationTestTemplates.Invoice(),
                ScheduledAtUtc: DateTime.UtcNow.AddMinutes(-1)
            )
        );
    }

    private async Task<CommunicationOutbox> RowAsync(Guid id)
    {
        await using var ctx = _db.Context();
        return await ctx.CommunicationOutbox.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == id);
    }

    private sealed class CapturingSender(Action<EmailMessage, CommunicationEmailSettings> capture) : IEmailSender
    {
        public Task<EmailDeliveryReceipt> SendAsync(EmailMessage message, CommunicationEmailSettings settings, CancellationToken ct = default)
        {
            capture(message, settings);
            return Task.FromResult(EmailDeliveryReceipt.WithoutProviderId);
        }
    }

    private sealed class InstanceResolver : ICommunicationSettingsResolver
    {
        private static readonly CommunicationEmailSettings Settings = new(true, "smtp.test", 587, null, null, "s@test.com", null, true, null, 3, "es");

        public Task<CommunicationEmailSettings> ResolveEmailAsync(CancellationToken ct = default) => Task.FromResult(Settings);

        public Task<CommunicationEmailSettings> ResolveEmailAsync(CommunicationScope scope, CancellationToken ct = default) => Task.FromResult(Settings);
    }
}
