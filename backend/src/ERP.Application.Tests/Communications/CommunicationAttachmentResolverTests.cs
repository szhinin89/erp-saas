using System.Text;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Communications.Services;
using ERP.Application.Modules.ElectronicDocuments.Communications;
using ERP.Application.Modules.Ride.Communications;
using ERP.Application.Modules.Ride.DTOs;
using ERP.Application.Modules.Ride.UseCases.GetOrGenerateRide;
using ERP.Domain.Modules.Communications.Constants;
using ERP.Domain.Modules.Communications.Entities;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Communications.ValueObjects;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.Communications;

/// <summary>
/// ZH-EDOC-COMMUNICATIONS-01 — adjuntos resueltos AL ENVIAR: bytes tal cual, rutas por IFileStorage
/// (nunca File.Exists del nodo), referencias por el módulo dueño (XML: ElectronicDocuments; RIDE: Ride).
/// </summary>
public sealed class CommunicationAttachmentResolverTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private const string AccessKeyValue = "2108202601179214672100110010010000000011234567811";

    [Fact]
    public async Task Bytes_se_envian_tal_cual_y_rutas_se_leen_del_almacenamiento_oficial()
    {
        var storage = new InMemoryStorage { ["tenant/x/file.txt"] = [1, 2, 3] };
        var message = Message();
        message.AddAttachment(
            CommunicationAttachmentType.Generic,
            "a.bin",
            "application/octet-stream",
            null,
            [9, 9],
            UserId
        );
        message.AddAttachment(
            CommunicationAttachmentType.Generic,
            "b.txt",
            "text/plain",
            "tenant/x/file.txt",
            null,
            UserId
        );

        var resolved = await Resolver(storage).ResolveAsync(message);

        resolved
            .Select(a => (a.FileName, a.Content))
            .Should()
            .BeEquivalentTo(
                new[] { ("a.bin", new byte[] { 9, 9 }), ("b.txt", new byte[] { 1, 2, 3 }) }
            );
    }

    [Fact]
    public async Task Ruta_inexistente_en_el_almacenamiento_es_fallo_reintentable()
    {
        var message = Message();
        message.AddAttachment(
            CommunicationAttachmentType.Generic,
            "b.txt",
            "text/plain",
            "no/existe.txt",
            null,
            UserId
        );

        var act = () => Resolver(new InMemoryStorage()).ResolveAsync(message);

        (await act.Should().ThrowAsync<CommunicationAttachmentException>())
            .Which.Code.Should()
            .Be(ApiResponseCodes.Communications.AttachmentUnavailable);
    }

    [Fact]
    public async Task Referencia_sin_proveedor_del_tipo_falla_cerrado()
    {
        var message = Message();
        message.AddAttachment(
            CommunicationAttachmentType.ReportPdf,
            "r.pdf",
            "application/pdf",
            null,
            null,
            UserId,
            Guid.NewGuid()
        );

        var act = () => Resolver(new InMemoryStorage()).ResolveAsync(message);

        await act.Should().ThrowAsync<CommunicationAttachmentException>();
    }

    [Fact]
    public async Task XML_autorizado_lo_entrega_ElectronicDocuments_byte_a_byte()
    {
        var document = AuthorizedDocument("edocs/aut.xml");
        var xml = Encoding
            .UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes("<factura>ñ</factura>"))
            .ToArray();
        var storage = new InMemoryStorage { ["edocs/aut.xml"] = xml };
        var message = Message();
        message.AddAttachment(
            CommunicationAttachmentType.AuthorizedXml,
            "f-autorizado.xml",
            "application/xml",
            null,
            null,
            UserId,
            document.Id
        );

        var resolved = await Resolver(storage, XmlProvider(document, storage))
            .ResolveAsync(message);

        resolved.Single().Content.Should().Equal(xml, "sin re-codificar: es el comprobante legal");
    }

    [Fact]
    public async Task XML_de_otra_empresa_o_ausente_no_se_adjunta_y_la_entrega_se_reintenta()
    {
        var document = AuthorizedDocument("edocs/aut.xml");
        var storage = new InMemoryStorage();
        var message = Message(otherCompany: true);
        message.AddAttachment(
            CommunicationAttachmentType.AuthorizedXml,
            "f.xml",
            "application/xml",
            null,
            null,
            UserId,
            document.Id
        );

        var act = () => Resolver(storage, XmlProvider(document, storage)).ResolveAsync(message);

        await act.Should().ThrowAsync<CommunicationAttachmentException>();
    }

    [Fact]
    public async Task RIDE_generado_por_Ride_se_adjunta_desde_su_almacenamiento()
    {
        var storage = new InMemoryStorage { ["ride/f.pdf"] = [37, 80, 68, 70] };
        var message = Message();
        message.AddAttachment(
            CommunicationAttachmentType.RidePdf,
            "f-RIDE.pdf",
            "application/pdf",
            null,
            null,
            UserId,
            Guid.NewGuid()
        );
        var (provider, sender) = RideProvider(
            storage,
            new RideGenerationResultDto(RideOutcome.Generated, "ride/f.pdf", null, null)
        );

        var resolved = await Resolver(storage, provider).ResolveAsync(message);

        resolved.Single().Content.Should().Equal(37, 80, 68, 70);
        sender.Verify(
            s =>
                s.Send(
                    It.Is<GetOrGenerateRideQuery>(q =>
                        q.SourceModule == "Sales" && q.SourceEntityId == message.SourceId
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task RIDE_fallido_se_omite_y_el_correo_sale_con_el_resto()
    {
        var storage = new InMemoryStorage();
        var message = Message();
        message.AddAttachment(
            CommunicationAttachmentType.RidePdf,
            "f-RIDE.pdf",
            "application/pdf",
            null,
            null,
            UserId,
            Guid.NewGuid()
        );
        var (provider, _) = RideProvider(
            storage,
            new RideGenerationResultDto(RideOutcome.Failed, null, null, "render_pipeline_error")
        );

        var resolved = await Resolver(storage, provider).ResolveAsync(message);

        resolved.Should().BeEmpty();
    }

    [Fact]
    public async Task RIDE_pendiente_de_fuente_es_reintentable()
    {
        var storage = new InMemoryStorage();
        var message = Message();
        message.AddAttachment(
            CommunicationAttachmentType.RidePdf,
            "f-RIDE.pdf",
            "application/pdf",
            null,
            null,
            UserId,
            Guid.NewGuid()
        );
        var (provider, _) = RideProvider(
            storage,
            new RideGenerationResultDto(RideOutcome.PendingSource, null, null, "source_xml_pending")
        );

        var act = () => Resolver(storage, provider).ResolveAsync(message);

        await act.Should().ThrowAsync<CommunicationAttachmentException>();
    }

    // ── helpers ───────────────────────────────────────────────────────────────────────────

    private static CommunicationAttachmentResolver Resolver(
        IFileStorage storage,
        params ICommunicationAttachmentContentProvider[] providers
    ) => new(storage, providers, NullLogger<CommunicationAttachmentResolver>.Instance);

    private static CommunicationOutbox Message(bool otherCompany = false)
    {
        var identity = CommunicationIdentity.For(
            CommunicationScope.Company(TenantId, otherCompany ? Guid.NewGuid() : CompanyId),
            CommunicationPurposes.SalesInvoiceAuthorized,
            CommunicationChannel.Email,
            new CommunicationSource("Sales", "SalesInvoice", Guid.NewGuid()),
            CommunicationRecipientRole.Customer
        );
        return CommunicationOutbox.CreateEmail(
            identity,
            "Cliente",
            "c@test.com",
            new CommunicationTemplateUsage(
                CommunicationPurposes.SalesInvoiceAuthorized,
                1,
                CommunicationTemplateSource.Default
            ),
            "Factura",
            "<p>x</p>",
            null,
            CommunicationPriority.Normal,
            null,
            3,
            UserId
        );
    }

    private static ElectronicDocument AuthorizedDocument(string xmlPath)
    {
        var document = ElectronicDocument.Create(
            TenantId,
            CompanyId,
            ElectronicDocumentType.Invoice,
            "Sales",
            Guid.NewGuid(),
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
            xmlPath,
            UserId
        );
        return document;
    }

    private static ElectronicDocumentAuthorizedXmlAttachmentProvider XmlProvider(
        ElectronicDocument document,
        IFileStorage storage
    )
    {
        var repository = new Mock<IElectronicDocumentRepository>();
        repository
            .Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        return new ElectronicDocumentAuthorizedXmlAttachmentProvider(repository.Object, storage);
    }

    private static (RidePdfCommunicationAttachmentProvider, Mock<ISender>) RideProvider(
        IFileStorage storage,
        RideGenerationResultDto ride
    )
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<GetOrGenerateRideQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RideGenerationResultDto>.Success(ride));
        return (new RidePdfCommunicationAttachmentProvider(sender.Object, storage), sender);
    }

    private sealed class InMemoryStorage : Dictionary<string, byte[]>, IFileStorage
    {
        public Task<string> SaveAsync(
            string relativePath,
            Stream content,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<Stream?> GetAsync(
            string storedPath,
            CancellationToken cancellationToken = default
        ) =>
            Task.FromResult<Stream?>(
                TryGetValue(storedPath, out var bytes) ? new MemoryStream(bytes) : null
            );

        public Task DeleteAsync(string storedPath, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
