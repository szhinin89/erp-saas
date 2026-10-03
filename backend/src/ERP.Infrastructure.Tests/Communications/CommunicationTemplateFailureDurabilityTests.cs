using ERP.Application.Common;
using ERP.Application.Modules.Communications.EventHandlers;
using ERP.Application.Modules.Communications.Services;
using ERP.Application.Modules.Communications.Templates;
using ERP.Application.Modules.Ride.DTOs;
using ERP.Application.Modules.Ride.UseCases.GetOrGenerateRide;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Communications.Constants;
using ERP.Domain.Modules.Communications.Entities;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Communications.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Events;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.ValueObjects;
using ERP.Infrastructure.Communications;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Configurations.Communications;
using ERP.Infrastructure.Persistence.Repositories.Communications;
using ERP.Infrastructure.Persistence.Repositories.ElectronicDocuments;
using ERP.Infrastructure.Services;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;

namespace ERP.Infrastructure.Tests.Communications;

/// <summary>
/// ZH-COMMUNICATIONS-TEMPLATES-01 (verificación final) — durabilidad ante fallo de template por el flujo
/// REAL: el ElectronicDocument autorizado se guarda con ErpDbContext, que publica
/// ElectronicDocumentAuthorizedEvent DENTRO de su transacción al handler real de Factura → cola real →
/// resolver real (override en PostgreSQL) → renderer → CommunicationOutbox. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class CommunicationTemplateFailureDurabilityTests
    : IClassFixture<CommunicationOutboxDeliveryIntegrationTests.Database>,
        IAsyncLifetime
{
    private const string AccessKeyValue = "2108202601179214672100110010010000000011234567811";
    private readonly CommunicationOutboxDeliveryIntegrationTests.Database _db;

    public CommunicationTemplateFailureDurabilityTests(CommunicationOutboxDeliveryIntegrationTests.Database db) => _db = db;

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
        var repeated = await RepublishAsync(document, invoice);
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

    // ── flujo real ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Autoriza y guarda el ElectronicDocument con ErpDbContext (transacción propia del SaveChanges): el
    /// evento se publica al handler real ANTES del commit, como en producción.
    /// </summary>
    private async Task<ElectronicDocument> AuthorizeInTransactionAsync(SalesInvoice invoice)
    {
        var document = AuthorizedDocument(invoice.Id);
        var publisher = new HandlerPublisher();
        await using var ctx = _db.Context(publisher: publisher);
        using var _ = JobExecutionContext.Begin(_db.TenantA, _db.CompanyA);
        publisher.Handler = BuildHandler(ctx, invoice);

        ctx.ElectronicDocuments.Add(document);
        await ctx.SaveChangesAsync();

        publisher.Delivered.Should().Be(1, "el handler real recibió ElectronicDocumentAuthorizedEvent");
        return document;
    }

    private async Task<bool> RepublishAsync(ElectronicDocument document, SalesInvoice invoice)
    {
        var queue = new Mock<ICommunicationQueue>();
        await using var ctx = _db.Context();
        using var _ = JobExecutionContext.Begin(_db.TenantA, _db.CompanyA);
        var realQueue = Queue(ctx);
        bool? wasAlreadyQueued = null;
        queue.Setup(q => q.EnqueueAsync(It.IsAny<CommunicationRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async (CommunicationRequest r, CancellationToken ct) =>
            {
                var result = await realQueue.EnqueueAsync(r, ct);
                wasAlreadyQueued = result.WasAlreadyQueued;
                return result;
            });

        await BuildHandler(ctx, invoice, queue.Object).Handle(
            new ElectronicDocumentAuthorizedEvent(_db.TenantA, document.Id, ElectronicDocumentType.Invoice, ElectronicDocumentState.Received, ElectronicDocumentState.Authorized),
            CancellationToken.None
        );
        return wasAlreadyQueued!.Value;
    }

    private SalesInvoiceAuthorizedCommunicationHandler BuildHandler(ErpDbContext ctx, SalesInvoice invoice, ICommunicationQueue? queue = null)
    {
        var invoices = new Mock<ISalesInvoiceRepository>();
        invoices.Setup(r => r.GetByIdAsync(_db.TenantA, invoice.Id, It.IsAny<CancellationToken>())).ReturnsAsync(invoice);

        var companies = new Mock<ICompanyRepository>();
        companies.Setup(r => r.GetByIdAsync(_db.CompanyA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Company.CreateManaged(_db.TenantA, "1790012345001", "Empresa A", tradeName: "ZH Demo"));

        var preferences = new Mock<IOperationalPreferencesResolver>();
        preferences.Setup(p => p.ResolveAsync(_db.TenantA, _db.CompanyA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperationalPreferences(
                SalesPos: new SalesPosPreferences(true, false, true, 0m, null, false, false, null, null),
                Cash: new CashPreferences(true, true, 0m, true, true, true),
                Purchases: new PurchasesPreferences(null, true, true, true, false),
                Inventory: new InventoryPreferences(false, true, false, 0m),
                Printing: new PrintingPreferences("AskBeforePrint", 1, "80mm", false, true, true, false),
                ElectronicDocuments: new ElectronicDocumentsPreferences(true, 3, true, EmailOnAuthorization: true),
                Notifications: new NotificationsPreferences(true, false, "es")
            ));

        var rideSender = new Mock<ISender>();
        rideSender.Setup(s => s.Send(It.IsAny<GetOrGenerateRideQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RideGenerationResultDto>.Success(new RideGenerationResultDto(RideOutcome.PendingSource, null, null, "pending")));

        return new SalesInvoiceAuthorizedCommunicationHandler(
            new ElectronicDocumentRepository(ctx, Mock.Of<ERP.Application.Common.Services.ICompanyClock>()),
            invoices.Object,
            companies.Object,
            queue ?? Queue(ctx),
            rideSender.Object,
            NullLogger<SalesInvoiceAuthorizedCommunicationHandler>.Instance,
            preferences.Object
        );
    }

    private static CommunicationQueue Queue(ErpDbContext ctx) =>
        new(
            new CommunicationOutboxRepository(ctx),
            new CurrentCompanyService(new HttpContextAccessor()),
            Mock.Of<ICurrentUser>(u => u.UserId == Guid.Empty),
            new FixedSettings(),
            new CommunicationTemplateResolver(new CommunicationTemplateRepository(ctx)),
            NullLogger<CommunicationQueue>.Instance
        );

    private async Task RunProcessorAsync(IEmailSender sender)
    {
        await using var ctx = _db.Context();
        await new CommunicationOutboxProcessor(
            new CommunicationOutboxDeliveryStore(ctx),
            sender,
            new FixedSettings(),
            TimeProvider.System,
            NullLogger<CommunicationOutboxProcessor>.Instance
        ).ProcessPendingAsync();
    }

    private async Task AddOverrideAsync(string subject)
    {
        await using var ctx = _db.Context();
        ctx.CommunicationTemplates.Add(CommunicationTemplate.Create(
            _db.TenantA, _db.CompanyA, null, CommunicationPurposes.SalesInvoiceAuthorized, "Factura personalizada",
            CommunicationChannel.Email, subject, "<p>{{CustomerName}}</p>", null, "es", Guid.Empty));
        await ctx.SaveChangesAsync();
    }

    private SalesInvoice AuthorizedInvoice()
    {
        var invoice = SalesInvoice.CreateDraft(
            _db.TenantA, _db.CompanyA, Guid.NewGuid(), Guid.NewGuid(),
            CustomerSnapshot.Create("Cliente Demo", "0102030405001", "04", "cliente@example.com"),
            "001-001-000000001", new DateOnly(2026, 8, 21), Guid.Empty,
            PaymentTermSnapshot.Create(Guid.NewGuid(), "Contado", installments: 1, daysBetween: 0), Guid.NewGuid()
        );
        invoice.ReplaceLines([SalesInvoiceDetail.Create(invoice.Id, _db.TenantA, "Producto", quantity: 1m, unitPrice: 100m, vatCode: "0", uomCode: "UNIT")], Guid.Empty);
        invoice.ReplacePayments([SalesInvoicePayment.Create(invoice.Id, _db.TenantA, Guid.NewGuid(), "01", "Efectivo", 100m)], Guid.Empty);
        invoice.Authorize(Guid.Empty);
        return invoice;
    }

    private ElectronicDocument AuthorizedDocument(Guid invoiceId)
    {
        var document = ElectronicDocument.Create(_db.TenantA, _db.CompanyA, ElectronicDocumentType.Invoice, "Sales", invoiceId, Guid.Empty);
        document.SetEnvironment("1");
        document.MarkXmlGenerated("edocs/draft.xml", "1.1.0", "1.1.0", Guid.Empty);
        document.MarkSigned("edocs/signed.xml", AccessKey.Create(AccessKeyValue), Guid.Empty);
        document.MarkSent(Guid.Empty);
        document.MarkReceived(Guid.Empty);
        document.MarkAuthorized(AuthorizationNumber.Create(AccessKeyValue), DateTime.UtcNow, "edocs/authorized.xml", Guid.Empty);
        return document;
    }

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

    /// <summary>Publisher del ErpDbContext que entrega ElectronicDocumentAuthorizedEvent al handler real.</summary>
    private sealed class HandlerPublisher : IPublisher
    {
        public SalesInvoiceAuthorizedCommunicationHandler? Handler { get; set; }
        public int Delivered { get; private set; }

        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            notification is INotification n ? Publish(n, cancellationToken) : Task.CompletedTask;

        public async Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            if (notification is ElectronicDocumentAuthorizedEvent authorized && Handler is not null)
            {
                Delivered++;
                await Handler.Handle(authorized, cancellationToken);
            }
        }
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

    private sealed class FixedSettings : ICommunicationSettingsResolver
    {
        private static readonly CommunicationEmailSettings Settings = new(true, "smtp.test", 587, null, null, "s@test.com", null, true, null, 3, "es");

        public Task<CommunicationEmailSettings> ResolveEmailAsync(CancellationToken ct = default) => Task.FromResult(Settings);

        public Task<CommunicationEmailSettings> ResolveEmailAsync(CommunicationScope scope, CancellationToken ct = default) => Task.FromResult(Settings);
    }
}
