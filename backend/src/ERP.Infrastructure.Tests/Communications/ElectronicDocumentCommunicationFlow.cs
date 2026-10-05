using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Communications.ElectronicDocuments;
using ERP.Application.Modules.Communications.EventHandlers;
using ERP.Application.Modules.Communications.Services;
using ERP.Application.Modules.Communications.Templates;
using ERP.Application.Modules.ElectronicDocuments.Communications;
using ERP.Application.Modules.Retentions.Communications;
using ERP.Application.Modules.Ride.Communications;
using ERP.Application.Modules.Ride.DTOs;
using ERP.Application.Modules.Ride.UseCases.GetOrGenerateRide;
using ERP.Application.Modules.Sales.Communications;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Communications.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Events;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.ValueObjects;
using ERP.Infrastructure.Communications;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Communications;
using ERP.Infrastructure.Persistence.Repositories.ElectronicDocuments;
using ERP.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Collections.Concurrent;
using System.Text;

namespace ERP.Infrastructure.Tests.Communications;

/// <summary>
/// ZH-EDOC-COMMUNICATIONS-01 — flujo REAL "comprobante autorizado → comunicación" contra PostgreSQL:
/// ErpDbContext publica ElectronicDocumentAuthorizedEvent DENTRO de su transacción al handler genérico
/// real → servicio → contributors reales → cola real → outbox. El envío usa el processor real con el
/// resolvedor de adjuntos real (XML por ElectronicDocuments sobre el repositorio real, RIDE por Ride).
/// Los documentos de negocio (factura, devolución, retención, terceros) se simulan en sus repositorios:
/// lo que se prueba aquí es la comunicación, no Ventas/Retenciones.
/// </summary>
internal sealed class ElectronicDocumentCommunicationFlow
{
    private readonly CommunicationOutboxDeliveryIntegrationTests.Database _db;
    private readonly ConcurrentDictionary<Guid, bool> _emailOnAuthorization = new();

    public ElectronicDocumentCommunicationFlow(CommunicationOutboxDeliveryIntegrationTests.Database db)
    {
        _db = db;
        Contacts.Setup(r => r.GetByBusinessPartnerAsync(It.IsAny<Guid>(), true, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        Locations.Setup(r => r.GetByBusinessPartnerAsync(It.IsAny<Guid>(), true, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        Companies.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
                Company.CreateManaged(id == db.CompanyA ? db.TenantA : db.TenantB, "1790012345001", "Empresa", tradeName: "ZH Demo"));
        Preferences.Setup(p => p.ResolveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid companyId, CancellationToken _) => PreferencesWith(_emailOnAuthorization.GetValueOrDefault(companyId, true)));
        RideSender.Setup(s => s.Send(It.IsAny<GetOrGenerateRideQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetOrGenerateRideQuery q, CancellationToken _) =>
            {
                var path = $"ride/{q.SourceModule}/{q.SourceEntityId}.pdf";
                Storage[path] = Encoding.ASCII.GetBytes($"%PDF-{q.SourceEntityId}");
                return Result<RideGenerationResultDto>.Success(new RideGenerationResultDto(RideOutcome.Generated, path, null, null));
            });
    }

    private static readonly Guid Actor = Guid.Parse("55555555-5555-5555-5555-555555555555");

    public InMemoryFileStorage Storage { get; } = new();
    public Mock<ISalesInvoiceRepository> Invoices { get; } = new();
    public Mock<ISalesReturnRepository> Returns { get; } = new();
    public Mock<IRetentionDocumentRepository> Retentions { get; } = new();
    public Mock<IBusinessPartnerRepository> Partners { get; } = new();
    public Mock<IBusinessPartnerContactRepository> Contacts { get; } = new();
    public Mock<IBusinessPartnerLocationRepository> Locations { get; } = new();
    public Mock<ICompanyRepository> Companies { get; } = new();
    public Mock<IOperationalPreferencesResolver> Preferences { get; } = new();
    public Mock<ISender> RideSender { get; } = new();

    /// <summary>Cursor de la reconciliación compartido por todas las corridas de este flujo (= un proceso).</summary>
    public ElectronicDocumentCommunicationReconciliationCursor ReconciliationCursor { get; } = new();

    public void DisableEmailOnAuthorization(Guid companyId) => _emailOnAuthorization[companyId] = false;

    // ── negocio simulado ──────────────────────────────────────────────────────────────────

    public SalesInvoice Invoice(Guid tenantId, Guid companyId, string? email, string number = "001-001-000000001")
    {
        var invoice = SalesInvoice.CreateDraft(
            tenantId, companyId, Guid.NewGuid(), Guid.NewGuid(),
            CustomerSnapshot.Create("Cliente Demo", "0102030405001", "04", email),
            number, new DateOnly(2026, 8, 21), Guid.Empty,
            PaymentTermSnapshot.Create(Guid.NewGuid(), "Contado", installments: 1, daysBetween: 0), Guid.NewGuid(),
            emissionType: EmissionType.Electronic
        );
        invoice.ReplaceLines([SalesInvoiceDetail.Create(invoice.Id, tenantId, "Producto", quantity: 1m, unitPrice: 100m, vatCode: "0", uomCode: "UNIT")], Guid.Empty);
        invoice.ReplacePayments([SalesInvoicePayment.Create(invoice.Id, tenantId, Guid.NewGuid(), "01", "Efectivo", 100m)], Guid.Empty);
        invoice.Authorize(Guid.Empty);
        Invoices.Setup(r => r.GetByIdAsync(tenantId, invoice.Id, It.IsAny<CancellationToken>())).ReturnsAsync(invoice);
        return invoice;
    }

    public SalesReturn CreditNote(SalesInvoice invoice, string creditNoteNumber = "001-001-000000009")
    {
        var original = invoice.Lines.First();
        var salesReturn = SalesReturn.CreateDraft(invoice.TenantId, invoice.CompanyId, invoice.Id, Guid.NewGuid(), "DEV-000001", "Producto en mal estado", Guid.Empty);
        salesReturn.AddLine(
            SalesReturnDetail.Create(salesReturn.Id, invoice.TenantId, original.Id, original.Description, 0.4m, original.UnitPrice, 0m, original.VatCode, original.VatRate, original.UomCode),
            Guid.Empty);
        salesReturn.AddRefundAllocation(SalesReturnRefundAllocation.Create(salesReturn.Id, invoice.TenantId, SalesReturnRefundMethod.Cash, salesReturn.GrandTotal), Guid.Empty);
        salesReturn.Authorize(Guid.Empty);
        salesReturn.SetCreditNoteDocumentNumber(creditNoteNumber);
        Returns.Setup(r => r.GetByIdAsync(invoice.TenantId, salesReturn.Id, It.IsAny<CancellationToken>())).ReturnsAsync(salesReturn);
        return salesReturn;
    }

    public RetentionDocument Retention(Guid tenantId, Guid companyId, string? supplierEmail)
    {
        var supplier = BusinessPartner.Create(tenantId, "04", "1791352688001", 2, "Proveedor Demo", Actor);
        Partners.Setup(r => r.GetByIdAsync(supplier.Id, It.IsAny<CancellationToken>())).ReturnsAsync(supplier);
        if (supplierEmail is not null)
            Contacts.Setup(r => r.GetByBusinessPartnerAsync(supplier.Id, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync([BusinessPartnerContact.Create(tenantId, supplier.Id, "Ana", ContactRole.Purchasing, Actor, email: supplierEmail, isPrimary: true)]);

        var retention = RetentionDocument.Create(tenantId, companyId, Guid.NewGuid(), RetentionSourceDocumentType.ExpenseDocument, Guid.NewGuid(), supplier.Id, Guid.NewGuid(), Actor);
        retention.AddLine(RetentionDocumentLine.Create(retention.Id, tenantId, RetentionTaxType.Vat, "725", "Retencion IVA 725", 10m, 30m, 3m));
        retention.Issue("001-001-000000007", new DateOnly(2026, 8, 27), Actor);
        Retentions.Setup(r => r.GetByIdAsync(tenantId, retention.Id, It.IsAny<CancellationToken>())).ReturnsAsync(retention);
        return retention;
    }

    // ── flujo real ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Autoriza y guarda el comprobante con ErpDbContext: el evento llega al handler genérico real ANTES
    /// del commit, como en producción. <paramref name="publish"/> = false simula que el evento se perdió
    /// (proceso caído / fallo absorbido): la reconciliación debe recuperarlo.
    /// </summary>
    public async Task<ElectronicDocument> AuthorizeAsync(
        Guid tenantId,
        Guid companyId,
        ElectronicDocumentType type,
        string sourceModule,
        Guid sourceEntityId,
        bool publish = true,
        bool storeAuthorizedXml = true
    )
    {
        var document = ElectronicDocument.Create(tenantId, companyId, type, sourceModule, sourceEntityId, Guid.Empty);
        var key = RandomAccessKey();
        var xmlPath = $"edocs/{tenantId}/{document.Id}-autorizado.xml";
        document.SetEnvironment("1");
        document.MarkXmlGenerated("edocs/draft.xml", "1.1.0", "1.1.0", Guid.Empty);
        document.MarkSigned("edocs/signed.xml", AccessKey.Create(key), Guid.Empty);
        document.MarkSent(Guid.Empty);
        document.MarkReceived(Guid.Empty);
        document.MarkAuthorized(AuthorizationNumber.Create(key), DateTime.UtcNow, storeAuthorizedXml ? xmlPath : null, Guid.Empty);
        if (storeAuthorizedXml)
            Storage[xmlPath] = Encoding.UTF8.GetBytes($"<autorizacion><numeroAutorizacion>{key}</numeroAutorizacion></autorizacion>");

        var publisher = new HandlerPublisher();
        await using var ctx = _db.Context(publisher: publisher);
        using var _ = JobExecutionContext.Begin(tenantId, companyId);
        if (publish)
            publisher.Handler = Handler(ctx);

        ctx.ElectronicDocuments.Add(document);
        await ctx.SaveChangesAsync();
        return document;
    }

    /// <summary>
    /// Envejece un comprobante ya guardado: su creación y su último cambio pasan a <paramref name="age"/>
    /// atrás (las marcas de tiempo las fija ErpDbContext al guardar; no hay otra forma de tener uno viejo).
    /// </summary>
    public async Task BackdateAsync(ElectronicDocument document, TimeSpan age)
    {
        await using var ctx = _db.Context();
        var at = DateTime.UtcNow - age;
        await ctx.Database.ExecuteSqlInterpolatedAsync($"UPDATE electronic_documents SET created_at = {at}, updated_at = {at} WHERE id = {document.Id}");
    }

    /// <summary>Re-entrega del mismo evento (duplicado) al handler real, fuera de la transacción original.</summary>
    public async Task RepublishAsync(ElectronicDocument document)
    {
        await using var ctx = _db.Context();
        using var _ = JobExecutionContext.Begin(document.TenantId, document.CompanyId);
        await Handler(ctx).Handle(
            new ElectronicDocumentAuthorizedEvent(document.TenantId, document.Id, document.DocumentType, ElectronicDocumentState.Received, ElectronicDocumentState.Authorized),
            CancellationToken.None);
    }

    public ElectronicDocumentAuthorizedCommunicationHandler Handler(ErpDbContext ctx) =>
        new(ElectronicDocuments(ctx), Service(ctx), NullLogger<ElectronicDocumentAuthorizedCommunicationHandler>.Instance);

    public ElectronicDocumentCommunicationService Service(ErpDbContext ctx) =>
        new(ContributorResolver(), Preferences.Object, Companies.Object, Queue(ctx), NullLogger<ElectronicDocumentCommunicationService>.Instance);

    public ElectronicDocumentCommunicationContributorResolver ContributorResolver() =>
        new([
            new SalesElectronicDocumentCommunicationContributor(Invoices.Object, Returns.Object),
            new RetentionElectronicDocumentCommunicationContributor(Retentions.Object, Partners.Object, Contacts.Object, Locations.Object),
        ]);

    public static CommunicationQueue Queue(ErpDbContext ctx) =>
        new(
            new CommunicationOutboxRepository(ctx),
            new CurrentCompanyService(new HttpContextAccessor()),
            Mock.Of<ICurrentUser>(u => u.UserId == Guid.Empty),
            new FixedSettings(),
            new CommunicationTemplateResolver(new CommunicationTemplateRepository(ctx)),
            NullLogger<CommunicationQueue>.Instance
        );

    /// <summary>Processor real con el resolvedor de adjuntos real (XML: ElectronicDocuments; RIDE: Ride).</summary>
    public async Task RunProcessorAsync(IEmailSender sender)
    {
        await using var ctx = _db.Context();
        await new CommunicationOutboxProcessor(
            new CommunicationOutboxDeliveryStore(ctx),
            sender,
            new CommunicationAttachmentResolver(
                Storage,
                [
                    new ElectronicDocumentAuthorizedXmlAttachmentProvider(ElectronicDocuments(ctx), Storage),
                    new RidePdfCommunicationAttachmentProvider(RideSender.Object, Storage),
                ],
                NullLogger<CommunicationAttachmentResolver>.Instance),
            new FixedSettings(),
            TimeProvider.System,
            NullLogger<CommunicationOutboxProcessor>.Instance
        ).ProcessPendingAsync();
    }

    /// <summary>Reconciliador real con DI real por scope (un ErpDbContext por scope, como en producción).</summary>
    public ElectronicDocumentCommunicationReconciler Reconciler(TimeSpan? clockOffset = null, int maxPerRun = ElectronicDocumentCommunicationReconciler.MaxPerRun)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _db.Context());
        services.AddScoped<IElectronicDocumentRepository>(sp => ElectronicDocuments(sp.GetRequiredService<ErpDbContext>()));
        services.AddScoped<IElectronicDocumentCommunicationReconciliationQuery>(sp =>
            new ElectronicDocumentCommunicationReconciliationQuery(sp.GetRequiredService<ErpDbContext>()));
        services.AddScoped<IElectronicDocumentCommunicationContributorResolver>(_ => ContributorResolver());
        services.AddScoped<IElectronicDocumentCommunicationService>(sp => Service(sp.GetRequiredService<ErpDbContext>()));
        services.AddSingleton(Preferences.Object);
        var provider = services.BuildServiceProvider();
        return new ElectronicDocumentCommunicationReconciler(
            provider.GetRequiredService<IServiceScopeFactory>(),
            ReconciliationCursor,
            new ShiftedTimeProvider(clockOffset ?? TimeSpan.FromMinutes(10)),
            NullLogger<ElectronicDocumentCommunicationReconciler>.Instance,
            maxPerRun);
    }

    private static ElectronicDocumentRepository ElectronicDocuments(ErpDbContext ctx) =>
        new(ctx, Mock.Of<ERP.Application.Common.Services.ICompanyClock>());

    private static string RandomAccessKey() =>
        string.Concat(Enumerable.Range(0, AccessKey.Length).Select(_ => (char)('0' + Random.Shared.Next(10))));

    private static OperationalPreferences PreferencesWith(bool emailOnAuthorization) =>
        new(
            SalesPos: new SalesPosPreferences(true, false, true, 0m, null, false, false, null, null),
            Cash: new CashPreferences(true, true, 0m, true, true, true),
            Purchases: new PurchasesPreferences(null, true, true, true, false),
            Inventory: new InventoryPreferences(false, true, false, 0m),
            Printing: new PrintingPreferences("AskBeforePrint", 1, "80mm", false, true, true, false),
            ElectronicDocuments: new ElectronicDocumentsPreferences(true, 3, true, emailOnAuthorization),
            Notifications: new NotificationsPreferences(true, false, "es")
        );

    /// <summary>Publisher del ErpDbContext que entrega ElectronicDocumentAuthorizedEvent al handler real.</summary>
    private sealed class HandlerPublisher : IPublisher
    {
        public ElectronicDocumentAuthorizedCommunicationHandler? Handler { get; set; }

        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            notification is INotification n ? Publish(n, cancellationToken) : Task.CompletedTask;

        public async Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            if (notification is ElectronicDocumentAuthorizedEvent authorized && Handler is not null)
                await Handler.Handle(authorized, cancellationToken);
        }
    }

    private sealed class ShiftedTimeProvider(TimeSpan offset) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + offset;
    }

    public sealed class FixedSettings : ICommunicationSettingsResolver
    {
        private static readonly CommunicationEmailSettings Settings = new(true, "smtp.test", 587, null, null, "s@test.com", null, true, null, 3, "es");

        public Task<CommunicationEmailSettings> ResolveEmailAsync(CancellationToken ct = default) => Task.FromResult(Settings);

        public Task<CommunicationEmailSettings> ResolveEmailAsync(CommunicationScope scope, CancellationToken ct = default) => Task.FromResult(Settings);
    }
}

/// <summary>Almacenamiento oficial simulado (IFileStorage): el envío nunca toca el filesystem del nodo.</summary>
internal sealed class InMemoryFileStorage : ConcurrentDictionary<string, byte[]>, IFileStorage
{
    public Task<string> SaveAsync(string relativePath, Stream content, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Stream?> GetAsync(string storedPath, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream?>(TryGetValue(storedPath, out var bytes) ? new MemoryStream(bytes) : null);

    public Task DeleteAsync(string storedPath, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>Sender de prueba que captura los mensajes (con adjuntos ya resueltos).</summary>
internal sealed class CapturingEmailSender : IEmailSender
{
    public ConcurrentBag<EmailMessage> Sent { get; } = [];

    public Task<EmailDeliveryReceipt> SendAsync(EmailMessage message, CommunicationEmailSettings settings, CancellationToken ct = default)
    {
        Sent.Add(message);
        return Task.FromResult(EmailDeliveryReceipt.WithoutProviderId);
    }
}

/// <summary>Resolvedor de adjuntos real sin proveedores (comunicaciones de prueba sin adjuntos).</summary>
internal static class NoAttachments
{
    public static ICommunicationAttachmentResolver Resolver { get; } =
        new CommunicationAttachmentResolver(new InMemoryFileStorage(), [], NullLogger<CommunicationAttachmentResolver>.Instance);
}
