using ERP.Application.Common;
using ERP.Application.Modules.Communications.EventHandlers;
using ERP.Application.Modules.Communications.DTOs;
using ERP.Application.Modules.Communications.Services;
using ERP.Application.Modules.Ride.DTOs;
using ERP.Application.Modules.Ride.UseCases.GetOrGenerateRide;
using ERP.Domain.Modules.Communications.Constants;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Events;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.ValueObjects;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;

namespace ERP.Application.Tests.Communications;

public sealed class SalesInvoiceAuthorizedCommunicationHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CompanyId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid BranchId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid UserId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task factura_autorizada_con_email_encola_correo_con_xml_ride_e_idempotencia()
    {
        var invoice = AuthorizedInvoice("cliente@example.com");
        var document = AuthorizedElectronicDocument(invoice.Id, authorizedXmlPath: "edocs/authorized.xml");
        var fixture = new Fixture(document, invoice);
        fixture.RideResult = Result<RideGenerationResultDto>.Success(
            new RideGenerationResultDto(
                RideOutcome.Generated,
                "ride/invoice.pdf",
                new RidePdfMetadataDto(
                    "Invoice",
                    "unversioned",
                    "unversioned",
                    "unversioned",
                    "hash",
                    DateTime.UtcNow,
                    WasCached: false
                ),
                ReasonCode: null
            )
        );

        await fixture.Handler.Handle(EventFor(document), CancellationToken.None);

        fixture.CapturedRequest.Should().NotBeNull();
        var request = fixture.CapturedRequest!;
        request.Purpose.Should().Be(CommunicationPurposes.SalesInvoiceAuthorized);
        request.RecipientEmail.Should().Be("cliente@example.com");
        request.RecipientName.Should().Be(invoice.Customer.Name);
        request.Subject.Should().Contain(invoice.InvoiceNumber);
        request.BodyText.Should().Contain(invoice.InvoiceNumber);
        request.BodyText.Should().Contain(document.AuthorizationNumber!.Value);
        request.BodyText.Should().Contain(invoice.Customer.Name);
        request.BodyText.Should().Contain("USD 100.00");
        request.BodyText.Should().Contain("ZH Demo");
        request.CorrelationType.Should().Be("SalesInvoice");
        request.CorrelationId.Should().Be(invoice.Id);
        request.BranchId.Should().Be(BranchId);
        request.SaveImmediately.Should().BeFalse();
        request.IdempotencyKey.Should().Contain(CommunicationPurposes.SalesInvoiceAuthorized);
        request.IdempotencyKey.Should().Contain(invoice.Id.ToString("N"));

        request.Attachments.Should().HaveCount(2);
        request.Attachments.Should().ContainSingle(a =>
            a.AttachmentType == CommunicationAttachmentType.AuthorizedXml
            && a.FileStoragePath == "edocs/authorized.xml"
            && a.ContentType == "application/xml"
        );
        request.Attachments.Should().ContainSingle(a =>
            a.AttachmentType == CommunicationAttachmentType.RidePdf
            && a.FileStoragePath == "ride/invoice.pdf"
            && a.ContentType == "application/pdf"
        );
    }

    [Fact]
    public async Task factura_autorizada_sin_email_no_encola_y_no_falla()
    {
        var invoice = AuthorizedInvoice(email: null);
        var document = AuthorizedElectronicDocument(invoice.Id, authorizedXmlPath: "edocs/authorized.xml");
        var fixture = new Fixture(document, invoice);

        var act = () => fixture.Handler.Handle(EventFor(document), CancellationToken.None);

        await act.Should().NotThrowAsync();
        fixture.Queue.Verify(
            q => q.QueueEmailAsync(It.IsAny<QueueEmailRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task reprocesar_misma_autorizacion_usa_misma_idempotency_key()
    {
        var invoice = AuthorizedInvoice("cliente@example.com");
        var document = AuthorizedElectronicDocument(invoice.Id, authorizedXmlPath: "edocs/authorized.xml");
        var fixture = new Fixture(document, invoice);

        await fixture.Handler.Handle(EventFor(document), CancellationToken.None);
        var firstKey = fixture.CapturedRequest!.IdempotencyKey;

        await fixture.Handler.Handle(EventFor(document), CancellationToken.None);
        var secondKey = fixture.CapturedRequest!.IdempotencyKey;

        secondKey.Should().Be(firstKey);
        fixture.Queue.Verify(
            q => q.QueueEmailAsync(
                It.Is<QueueEmailRequest>(r => r.IdempotencyKey == firstKey),
                It.IsAny<CancellationToken>()
            ),
            Times.Exactly(2)
        );
    }

    [Fact]
    public async Task fallo_de_ride_no_bloquea_el_encolado_con_xml()
    {
        var invoice = AuthorizedInvoice("cliente@example.com");
        var document = AuthorizedElectronicDocument(invoice.Id, authorizedXmlPath: "edocs/authorized.xml");
        var fixture = new Fixture(document, invoice)
        {
            RideResult = Result<RideGenerationResultDto>.Success(
                new RideGenerationResultDto(
                    RideOutcome.Failed,
                    StoragePath: null,
                    Metadata: null,
                    ReasonCode: "render_pipeline_error"
                )
            ),
        };

        await fixture.Handler.Handle(EventFor(document), CancellationToken.None);

        fixture.CapturedRequest.Should().NotBeNull();
        fixture.CapturedRequest!.Attachments.Should().ContainSingle(a =>
            a.AttachmentType == CommunicationAttachmentType.AuthorizedXml
        );
        fixture.CapturedRequest.Attachments.Should().NotContain(a =>
            a.AttachmentType == CommunicationAttachmentType.RidePdf
        );
        invoice.Status.Should().Be(SalesInvoiceStatus.Authorized);
    }

    private static SalesInvoice AuthorizedInvoice(string? email)
    {
        var invoice = SalesInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            Guid.NewGuid(),
            CustomerSnapshot.Create("Cliente Demo", "0102030405001", "04", email),
            "001-001-000000001",
            new DateOnly(2026, 8, 21),
            UserId,
            PaymentTermSnapshot.Create(Guid.NewGuid(), "Contado", installments: 1, daysBetween: 0),
            Guid.NewGuid()
        );
        var line = SalesInvoiceDetail.Create(
            invoice.Id,
            TenantId,
            "Producto demo",
            quantity: 1m,
            unitPrice: 100m,
            vatCode: "0",
            uomCode: "UNIT"
        );
        var payment = SalesInvoicePayment.Create(
            invoice.Id,
            TenantId,
            Guid.NewGuid(),
            "01",
            "Efectivo",
            100m
        );
        invoice.ReplaceLines([line], UserId);
        invoice.ReplacePayments([payment], UserId);
        invoice.Authorize(UserId);
        return invoice;
    }

    private static ElectronicDocument AuthorizedElectronicDocument(
        Guid invoiceId,
        string? authorizedXmlPath
    )
    {
        var document = ElectronicDocument.Create(
            TenantId,
            CompanyId,
            ElectronicDocumentType.Invoice,
            "Sales",
            invoiceId,
            UserId
        );
        document.SetEnvironment("1");
        document.MarkXmlGenerated("edocs/draft.xml", "1.1.0", "1.1.0", UserId);
        document.MarkSigned("edocs/signed.xml", AccessKey.Create(AccessKeyValue), UserId);
        document.MarkSent(UserId);
        document.MarkReceived(UserId);
        document.MarkAuthorized(
            AuthorizationNumber.Create(AccessKeyValue),
            DateTime.UtcNow,
            authorizedXmlPath,
            UserId
        );
        return document;
    }

    private static ElectronicDocumentAuthorizedEvent EventFor(ElectronicDocument document) =>
        new(
            document.TenantId,
            document.Id,
            document.DocumentType,
            ElectronicDocumentState.Received,
            ElectronicDocumentState.Authorized
        );

    private const string AccessKeyValue =
        "2108202601179214672100110010010000000011234567811";

    private sealed class Fixture
    {
        public Mock<ICommunicationQueue> Queue { get; } = new();
        public QueueEmailRequest? CapturedRequest { get; private set; }
        public Result<RideGenerationResultDto> RideResult { get; set; } =
            Result<RideGenerationResultDto>.Success(
                new RideGenerationResultDto(
                    RideOutcome.PendingSource,
                    StoragePath: null,
                    Metadata: null,
                    ReasonCode: "source_xml_pending"
                )
            );


        public SalesInvoiceAuthorizedCommunicationHandler Handler { get; }

        public Fixture(ElectronicDocument document, SalesInvoice invoice)
        {
            var electronicDocuments = new Mock<IElectronicDocumentRepository>();
            electronicDocuments
                .Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(document);

            var salesInvoices = new Mock<ISalesInvoiceRepository>();
            salesInvoices
                .Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(invoice);

            var companies = new Mock<ICompanyRepository>();
            companies
                .Setup(r => r.GetByIdAsync(CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    Company.CreateManaged(
                        TenantId,
                        "1792146721001",
                        "ZH Technologies S.A.",
                        tradeName: "ZH Demo"
                    )
                );

            Queue
                .Setup(q => q.QueueEmailAsync(It.IsAny<QueueEmailRequest>(), It.IsAny<CancellationToken>()))
                .Callback<QueueEmailRequest, CancellationToken>((request, _) => CapturedRequest = request)
                .ReturnsAsync(new QueuedCommunicationDto(Guid.NewGuid(), WasAlreadyQueued: false));

            var sender = new Mock<ISender>();
            sender
                .Setup(s => s.Send(It.IsAny<GetOrGenerateRideQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => RideResult);

            Handler = new SalesInvoiceAuthorizedCommunicationHandler(
                electronicDocuments.Object,
                salesInvoices.Object,
                companies.Object,
                Queue.Object,
                sender.Object,
                Mock.Of<ILogger<SalesInvoiceAuthorizedCommunicationHandler>>()
            );
        }
    }
}
