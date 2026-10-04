using ERP.Domain.Modules.Company.Enums;
using ERP.Application.Common;
using ERP.Application.Modules.Communications.DTOs;
using ERP.Application.Modules.Communications.ElectronicDocuments;
using ERP.Application.Modules.Communications.EventHandlers;
using ERP.Application.Modules.Communications.Services;
using ERP.Application.Modules.Communications.Templates;
using ERP.Application.Modules.Retentions.Communications;
using ERP.Application.Modules.Sales.Communications;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Communications.Constants;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Communications.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
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
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.Communications;

/// <summary>
/// ZH-EDOC-COMMUNICATIONS-01 — handler genérico "comprobante autorizado → comunicación" con los
/// contributors reales de Ventas y Retenciones (repositorios simulados). Sustituye a los tests del
/// handler específico de factura y conserva su golden.
/// </summary>
public sealed class ElectronicDocumentAuthorizedCommunicationHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CompanyId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid BranchId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid UserId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private const string AccessKeyValue = "2108202601179214672100110010010000000011234567811";

    public static TheoryData<string> GoldenCases() => new() { nameof(SalesInvoiceAuthorizedGoldenEmail.Plain), nameof(SalesInvoiceAuthorizedGoldenEmail.Special) };

    // ── Factura: regresión (golden) ───────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(GoldenCases))]
    public async Task Factura_reproduce_exactamente_la_salida_golden(string goldenCase)
    {
        var golden = goldenCase == nameof(SalesInvoiceAuthorizedGoldenEmail.Plain)
            ? SalesInvoiceAuthorizedGoldenEmail.Plain
            : SalesInvoiceAuthorizedGoldenEmail.Special;
        var fixture = new Fixture();
        var invoice = fixture.AddInvoice("cliente@example.com", golden.CustomerName);
        var document = fixture.AddDocument(ElectronicDocumentType.Invoice, "Sales", invoice.Id);

        await fixture.Handler.Handle(EventFor(document), CancellationToken.None);

        var (subject, html, text) = RenderedOf(CommunicationDefaultTemplates.SalesInvoiceAuthorizedV1, fixture.Single);
        subject.Should().Be(golden.Subject);
        html.Should().Be(golden.Html);
        text.Should().Be(golden.Text);
    }

    [Fact]
    public async Task Factura_encola_con_alcance_origen_rol_y_adjuntos_por_referencia()
    {
        var fixture = new Fixture();
        var invoice = fixture.AddInvoice("cliente@example.com");
        var document = fixture.AddDocument(ElectronicDocumentType.Invoice, "Sales", invoice.Id);

        await fixture.Handler.Handle(EventFor(document), CancellationToken.None);

        var request = fixture.Single;
        request.Purpose.Should().Be(CommunicationPurposes.SalesInvoiceAuthorized);
        request.Scope.Should().Be(CommunicationScope.Company(TenantId, CompanyId, BranchId));
        request.Source.Should().Be(new CommunicationSource("Sales", "SalesInvoice", invoice.Id));
        request.RecipientRole.Should().Be(CommunicationRecipientRole.Customer);
        request.RecipientEmail.Should().Be("cliente@example.com");
        request.MaxRetries.Should().BeNull("se copia del perfil resuelto al encolar");
        request.Template.Should().Be(new SalesInvoiceAuthorizedTemplateModel(
            invoice.Customer.Name, invoice.InvoiceNumber, AccessKeyValue, "100.00", "ZH Demo"));

        // Adjuntos POR REFERENCIA al comprobante: nada de rutas locales, bytes ni RIDE generado aquí.
        request.Attachments.Should().BeEquivalentTo(new[]
        {
            new QueueCommunicationAttachmentDto(CommunicationAttachmentType.AuthorizedXml, "001-001-000000001-autorizado.xml", "application/xml", ReferenceId: document.Id),
            new QueueCommunicationAttachmentDto(CommunicationAttachmentType.RidePdf, "001-001-000000001-RIDE.pdf", "application/pdf", ReferenceId: document.Id),
        });
    }

    [Fact]
    public async Task Sin_XML_autorizado_almacenado_solo_referencia_el_RIDE()
    {
        var fixture = new Fixture();
        var invoice = fixture.AddInvoice("cliente@example.com");
        var document = fixture.AddDocument(ElectronicDocumentType.Invoice, "Sales", invoice.Id, authorizedXmlPath: null);

        await fixture.Handler.Handle(EventFor(document), CancellationToken.None);

        fixture.Single.Attachments!.Select(a => a.AttachmentType).Should().Equal(CommunicationAttachmentType.RidePdf);
    }

    [Fact]
    public async Task Factura_sin_email_encola_igual_y_la_cola_deja_la_evidencia()
    {
        var fixture = new Fixture();
        var invoice = fixture.AddInvoice(email: null);
        var document = fixture.AddDocument(ElectronicDocumentType.Invoice, "Sales", invoice.Id);

        await fixture.Handler.Handle(EventFor(document), CancellationToken.None);

        fixture.Single.RecipientEmail.Should().BeNull("el contributor no inventa un correo: la cola registra RECIPIENT_MISSING");
    }

    [Fact]
    public async Task Preferencia_desactivada_no_encola()
    {
        var fixture = new Fixture(emailOnAuthorization: false);
        var invoice = fixture.AddInvoice("cliente@example.com");
        var document = fixture.AddDocument(ElectronicDocumentType.Invoice, "Sales", invoice.Id);

        await fixture.Handler.Handle(EventFor(document), CancellationToken.None);

        fixture.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Evento_duplicado_produce_la_misma_identidad()
    {
        var fixture = new Fixture();
        var invoice = fixture.AddInvoice("cliente@example.com");
        var document = fixture.AddDocument(ElectronicDocumentType.Invoice, "Sales", invoice.Id);

        await fixture.Handler.Handle(EventFor(document), CancellationToken.None);
        await fixture.Handler.Handle(EventFor(document), CancellationToken.None);

        fixture.Requests.Should().HaveCount(2);
        fixture.Requests.Select(KeyOf).Distinct().Should().ContainSingle("la outbox decide: misma identidad, una sola fila");
    }

    // ── Nota de crédito ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Nota_de_credito_va_al_cliente_de_la_factura_con_su_template()
    {
        var fixture = new Fixture();
        var invoice = fixture.AddInvoice("cliente@example.com");
        var salesReturn = fixture.AddReturn(invoice);
        var document = fixture.AddDocument(ElectronicDocumentType.CreditNote, "Sales", salesReturn.Id);

        await fixture.Handler.Handle(EventFor(document), CancellationToken.None);

        var request = fixture.Single;
        request.Purpose.Should().Be(CommunicationPurposes.SalesCreditNoteAuthorized);
        request.Source.Should().Be(new CommunicationSource("Sales", "SalesReturn", salesReturn.Id));
        request.RecipientRole.Should().Be(CommunicationRecipientRole.Customer);
        request.RecipientEmail.Should().Be("cliente@example.com");
        request.Template.Should().Be(new SalesCreditNoteAuthorizedTemplateModel(
            invoice.Customer.Name, "001-001-000000009", invoice.InvoiceNumber, AccessKeyValue, "40.00", "ZH Demo"));
        request.Attachments!.Select(a => a.FileName).Should().Equal("001-001-000000009-autorizado.xml", "001-001-000000009-RIDE.pdf");

        var (subject, _, text) = RenderedOf(CommunicationDefaultTemplates.SalesCreditNoteAuthorizedV1, request);
        subject.Should().Be("Nota de credito autorizada 001-001-000000009 - ZH Demo");
        text.Should().Contain("Factura modificada: 001-001-000000001").And.Contain("Total: USD 40.00");
    }

    [Fact]
    public async Task Nota_de_credito_no_autorizada_en_Ventas_no_encola()
    {
        var fixture = new Fixture();
        var invoice = fixture.AddInvoice("cliente@example.com");
        var salesReturn = fixture.AddReturn(invoice, authorize: false);
        var document = fixture.AddDocument(ElectronicDocumentType.CreditNote, "Sales", salesReturn.Id);

        await fixture.Handler.Handle(EventFor(document), CancellationToken.None);

        fixture.Requests.Should().BeEmpty();
    }

    // ── Retención ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Retencion_va_al_sujeto_retenido_con_el_correo_de_su_contacto()
    {
        var fixture = new Fixture();
        var retention = fixture.AddRetention(contactEmail: "proveedor@example.com");
        var document = fixture.AddDocument(ElectronicDocumentType.Retention, "Retentions", retention.Id);

        await fixture.Handler.Handle(EventFor(document), CancellationToken.None);

        var request = fixture.Single;
        request.Purpose.Should().Be(CommunicationPurposes.RetentionAuthorized);
        request.Source.Should().Be(new CommunicationSource("Retentions", "RetentionDocument", retention.Id));
        request.RecipientRole.Should().Be(CommunicationRecipientRole.Supplier);
        request.RecipientName.Should().Be("Proveedor Demo");
        request.RecipientEmail.Should().Be("proveedor@example.com");
        request.Scope.Should().Be(CommunicationScope.Company(TenantId, CompanyId, BranchId));
        request.Template.Should().Be(new RetentionAuthorizedTemplateModel(
            "Proveedor Demo", "001-001-000000007", "-", AccessKeyValue, "3.00", "ZH Demo"));

        var (subject, _, _) = RenderedOf(CommunicationDefaultTemplates.RetentionAuthorizedV1, request);
        subject.Should().Be("Comprobante de retencion autorizado 001-001-000000007 - ZH Demo");
    }

    [Fact]
    public async Task Retencion_sin_correo_de_contacto_no_inventa_uno()
    {
        var fixture = new Fixture();
        var retention = fixture.AddRetention(contactEmail: null);
        var document = fixture.AddDocument(ElectronicDocumentType.Retention, "Retentions", retention.Id);

        await fixture.Handler.Handle(EventFor(document), CancellationToken.None);

        fixture.Single.RecipientEmail.Should().BeNull();
    }

    // ── Enrutamiento y robustez ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ElectronicDocumentType.DebitNote, "Sales")]
    [InlineData(ElectronicDocumentType.Invoice, "Purchases")]
    public async Task Origen_sin_contributor_se_omite_sin_encolar(ElectronicDocumentType type, string module)
    {
        var fixture = new Fixture();
        var document = fixture.AddDocument(type, module, Guid.NewGuid());

        await fixture.Handler.Handle(EventFor(document), CancellationToken.None);

        fixture.Requests.Should().BeEmpty();
        fixture.Preferences.Verify(p => p.ResolveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Documento_de_origen_inexistente_no_encola()
    {
        var fixture = new Fixture();
        var document = fixture.AddDocument(ElectronicDocumentType.Invoice, "Sales", Guid.NewGuid());

        var result = await fixture.Service.RequestAsync(document, ElectronicDocumentCommunicationTrigger.Reconciliation);

        result.Outcome.Should().Be(ElectronicDocumentCommunicationOutcome.Skipped);
        result.FailureCode.Should().Be(ApiResponseCodes.Communications.SourceNotFound);
        fixture.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Un_fallo_inesperado_no_se_propaga_a_la_autorizacion()
    {
        var fixture = new Fixture();
        var invoice = fixture.AddInvoice("cliente@example.com");
        var document = fixture.AddDocument(ElectronicDocumentType.Invoice, "Sales", invoice.Id);
        fixture.Queue.Setup(q => q.EnqueueAsync(It.IsAny<CommunicationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var act = () => fixture.Handler.Handle(EventFor(document), CancellationToken.None);

        await act.Should().NotThrowAsync("la autorización SRI ya ocurrió; la reconciliación recupera");
    }

    [Fact]
    public void Dos_contributors_para_la_misma_ruta_es_un_error_de_composicion()
    {
        var sales = new SalesElectronicDocumentCommunicationContributor(Mock.Of<ISalesInvoiceRepository>(), Mock.Of<ISalesReturnRepository>());

        var act = () => new ElectronicDocumentCommunicationContributorResolver([sales, sales]);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Las_rutas_son_las_tres_del_alcance_con_sus_propositos()
    {
        new Fixture().Resolver.Routes.Should().BeEquivalentTo(new[]
        {
            new ElectronicDocumentCommunicationRoute("Sales", ElectronicDocumentType.Invoice, "SalesInvoice", CommunicationPurposes.SalesInvoiceAuthorized),
            new ElectronicDocumentCommunicationRoute("Sales", ElectronicDocumentType.CreditNote, "SalesReturn", CommunicationPurposes.SalesCreditNoteAuthorized),
            new ElectronicDocumentCommunicationRoute("Retentions", ElectronicDocumentType.Retention, "RetentionDocument", CommunicationPurposes.RetentionAuthorized),
        });
    }

    // ── helpers ───────────────────────────────────────────────────────────────────────────

    private static (string Subject, string? Html, string? Text) RenderedOf(CommunicationTemplateDefinition definition, CommunicationRequest request)
    {
        var rendered = CommunicationTemplateRenderer.Render(definition, request.Template);
        rendered.IsSuccess.Should().BeTrue(rendered.Error);
        return (rendered.Value!.Subject, rendered.Value.Html, rendered.Value.Text);
    }

    private static ElectronicDocumentAuthorizedEvent EventFor(ElectronicDocument document) =>
        new(document.TenantId, document.Id, document.DocumentType, ElectronicDocumentState.Received, ElectronicDocumentState.Authorized);

    private static string KeyOf(CommunicationRequest request) =>
        CommunicationIdentity.For(request.Scope, request.Purpose, request.Channel, request.Source, request.RecipientRole).Key;

    private sealed class Fixture
    {
        private readonly Mock<IElectronicDocumentRepository> _documents = new();
        private readonly Mock<ISalesInvoiceRepository> _invoices = new();
        private readonly Mock<ISalesReturnRepository> _returns = new();
        private readonly Mock<IRetentionDocumentRepository> _retentions = new();
        private readonly Mock<IBusinessPartnerRepository> _partners = new();
        private readonly Mock<IBusinessPartnerContactRepository> _contacts = new();
        private readonly Mock<IBusinessPartnerLocationRepository> _locations = new();

        public Mock<ICommunicationQueue> Queue { get; } = new();
        public Mock<IOperationalPreferencesResolver> Preferences { get; } = new();
        public List<CommunicationRequest> Requests { get; } = [];
        public CommunicationRequest Single => Requests.Should().ContainSingle().Subject;
        public ElectronicDocumentCommunicationContributorResolver Resolver { get; }
        public ElectronicDocumentCommunicationService Service { get; }
        public ElectronicDocumentAuthorizedCommunicationHandler Handler { get; }

        public Fixture(bool emailOnAuthorization = true)
        {
            Preferences
                .Setup(p => p.ResolveAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OperationalPreferences(
                    SalesPos: new SalesPosPreferences(true, false, true, 0m, null, false, false, null, null),
                    Cash: new CashPreferences(true, true, 0m, true, true, true),
                    Purchases: new PurchasesPreferences(null, true, true, true, false),
                    Inventory: new InventoryPreferences(false, true, false, 0m),
                    Printing: new PrintingPreferences("AskBeforePrint", 1, "80mm", false, true, true, false),
                    ElectronicDocuments: new ElectronicDocumentsPreferences(true, 3, true, emailOnAuthorization),
                    Notifications: new NotificationsPreferences(true, false, "es")
                ));

            var companies = new Mock<ICompanyRepository>();
            companies
                .Setup(r => r.GetByIdAsync(CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Company.CreateManaged(TenantId, "1792146721001", "ZH Technologies S.A.", tradeName: "ZH Demo"));

            Queue
                .Setup(q => q.EnqueueAsync(It.IsAny<CommunicationRequest>(), It.IsAny<CancellationToken>()))
                .Callback<CommunicationRequest, CancellationToken>((request, _) => Requests.Add(request))
                .ReturnsAsync(() => new QueuedCommunicationDto(Guid.NewGuid(), WasAlreadyQueued: false));

            _contacts.Setup(r => r.GetByBusinessPartnerAsync(It.IsAny<Guid>(), true, It.IsAny<CancellationToken>())).ReturnsAsync([]);
            _locations.Setup(r => r.GetByBusinessPartnerAsync(It.IsAny<Guid>(), true, It.IsAny<CancellationToken>())).ReturnsAsync([]);

            Resolver = new ElectronicDocumentCommunicationContributorResolver([
                new SalesElectronicDocumentCommunicationContributor(_invoices.Object, _returns.Object),
                new RetentionElectronicDocumentCommunicationContributor(_retentions.Object, _partners.Object, _contacts.Object, _locations.Object),
            ]);
            Service = new ElectronicDocumentCommunicationService(
                Resolver, Preferences.Object, companies.Object, Queue.Object, NullLogger<ElectronicDocumentCommunicationService>.Instance);
            Handler = new ElectronicDocumentAuthorizedCommunicationHandler(
                _documents.Object, Service, NullLogger<ElectronicDocumentAuthorizedCommunicationHandler>.Instance);
        }

        public SalesInvoice AddInvoice(string? email, string customerName = "Cliente Demo")
        {
            var invoice = SalesInvoice.CreateDraft(
                TenantId, CompanyId, BranchId, Guid.NewGuid(),
                CustomerSnapshot.Create(customerName, "0102030405001", "04", email),
                "001-001-000000001", new DateOnly(2026, 8, 21), UserId,
                PaymentTermSnapshot.Create(Guid.NewGuid(), "Contado", installments: 1, daysBetween: 0),
                Guid.NewGuid(),
                emissionType: EmissionType.Electronic
            );
            var line = SalesInvoiceDetail.Create(invoice.Id, TenantId, "Producto demo", quantity: 1m, unitPrice: 100m, vatCode: "0", uomCode: "UNIT");
            invoice.ReplaceLines([line], UserId);
            invoice.ReplacePayments([SalesInvoicePayment.Create(invoice.Id, TenantId, Guid.NewGuid(), "01", "Efectivo", 100m)], UserId);
            invoice.Authorize(UserId);
            _invoices.Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>())).ReturnsAsync(invoice);
            return invoice;
        }

        public SalesReturn AddReturn(SalesInvoice invoice, bool authorize = true)
        {
            var original = invoice.Lines.First();
            var salesReturn = SalesReturn.CreateDraft(TenantId, CompanyId, invoice.Id, Guid.NewGuid(), "DEV-000001", "Producto en mal estado", UserId);
            salesReturn.AddLine(
                SalesReturnDetail.Create(salesReturn.Id, TenantId, original.Id, original.Description, 0.4m, original.UnitPrice, 0m, original.VatCode, original.VatRate, original.UomCode),
                UserId
            );
            if (authorize)
            {
                salesReturn.AddRefundAllocation(
                    SalesReturnRefundAllocation.Create(salesReturn.Id, TenantId, SalesReturnRefundMethod.Cash, salesReturn.GrandTotal), UserId);
                salesReturn.Authorize(UserId);
                salesReturn.SetCreditNoteDocumentNumber("001-001-000000009");
            }
            _returns.Setup(r => r.GetByIdAsync(TenantId, salesReturn.Id, It.IsAny<CancellationToken>())).ReturnsAsync(salesReturn);
            return salesReturn;
        }

        public RetentionDocument AddRetention(string? contactEmail)
        {
            var supplier = BusinessPartner.Create(TenantId, "04", "1791352688001", 2, "Proveedor Demo", UserId);
            _partners.Setup(r => r.GetByIdAsync(supplier.Id, It.IsAny<CancellationToken>())).ReturnsAsync(supplier);
            if (contactEmail is not null)
                _contacts
                    .Setup(r => r.GetByBusinessPartnerAsync(supplier.Id, true, It.IsAny<CancellationToken>()))
                    .ReturnsAsync([BusinessPartnerContact.Create(TenantId, supplier.Id, "Ana", ContactRole.Purchasing, UserId, email: contactEmail, isPrimary: true)]);

            var retention = RetentionDocument.Create(
                TenantId, CompanyId, BranchId, RetentionSourceDocumentType.ExpenseDocument, Guid.NewGuid(), supplier.Id, Guid.NewGuid(), UserId);
            retention.AddLine(RetentionDocumentLine.Create(retention.Id, TenantId, RetentionTaxType.Vat, "725", "Retencion IVA 725", 10m, 30m, 3m));
            retention.Issue("001-001-000000007", new DateOnly(2026, 8, 27), UserId);
            _retentions.Setup(r => r.GetByIdAsync(TenantId, retention.Id, It.IsAny<CancellationToken>())).ReturnsAsync(retention);
            return retention;
        }

        public ElectronicDocument AddDocument(
            ElectronicDocumentType type,
            string sourceModule,
            Guid sourceEntityId,
            string? authorizedXmlPath = "edocs/authorized.xml"
        )
        {
            var document = ElectronicDocument.Create(TenantId, CompanyId, type, sourceModule, sourceEntityId, UserId);
            document.SetEnvironment("1");
            document.MarkXmlGenerated("edocs/draft.xml", "1.1.0", "1.1.0", UserId);
            document.MarkSigned("edocs/signed.xml", AccessKey.Create(AccessKeyValue), UserId);
            document.MarkSent(UserId);
            document.MarkReceived(UserId);
            document.MarkAuthorized(AuthorizationNumber.Create(AccessKeyValue), DateTime.UtcNow, authorizedXmlPath, UserId);
            _documents.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>())).ReturnsAsync(document);
            return document;
        }
    }
}
