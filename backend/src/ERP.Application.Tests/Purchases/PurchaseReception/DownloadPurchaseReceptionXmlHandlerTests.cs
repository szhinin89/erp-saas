using ERP.Application.Common;
using ERP.Application.Modules.Purchases.PurchaseReception.Services;
using ERP.Application.Modules.Purchases.PurchaseReception.UseCases.DownloadPurchaseReceptionXml;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ERP.Application.Tests.Purchases.PurchaseReception;

/// <summary>
/// Cubre exclusivamente responsabilidades del handler (descarga SRI, transición de estado,
/// persistencia atómica). La interpretación del detalle XML + Item Matching ahora vive en
/// <see cref="PurchaseReceptionDetailProcessor"/> (compartida con el reprocesamiento manual) y se
/// prueba por separado en <c>PurchaseReceptionDetailProcessorTests</c>.
/// </summary>
public sealed class DownloadPurchaseReceptionXmlHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static PurchaseReceptionDocument SampleDocument(Guid? supplierId = null) =>
        PurchaseReceptionDocument.Create(
            TenantId,
            CompanyId,
            BranchId,
            PurchaseReceptionSourceDocType.Invoice,
            "1791352688001",
            "QUALA ECUADOR S A",
            supplierId,
            "0107202601179135268800120150270001617400016174011",
            "015-027-000161740",
            new DateOnly(2026, 7, 1),
            new DateTime(2026, 7, 1, 21, 6, 55, DateTimeKind.Utc),
            15.96m,
            2.4m,
            18.35m,
            UserId
        );

    private static PurchaseReceptionDetailProcessingResult FailedResult(
        string note = "XML sin detalle de prueba."
    ) => new([], PurchaseReceptionProcessingOutcome.Failed(note), null, null);

    private static (
        DownloadPurchaseReceptionXmlHandler handler,
        Mock<IPurchaseReceptionDocumentRepository> repo,
        Mock<ISriReceptionXmlProvider> provider,
        Mock<IPurchaseReceptionDetailProcessor> detailProcessor
    ) BuildHandler()
    {
        var repo = new Mock<IPurchaseReceptionDocumentRepository>();
        var provider = new Mock<ISriReceptionXmlProvider>();
        var detailProcessor = new Mock<IPurchaseReceptionDetailProcessor>();

        // Por defecto: sin detalle interpretable — los tests que no ejercitan el procesamiento
        // quedan enfocados exclusivamente en el ciclo de descarga/verificación del handler.
        detailProcessor
            .Setup(p =>
                p.ProcessAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(FailedResult());

        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);
        var logger = new Mock<ILogger<DownloadPurchaseReceptionXmlHandler>>();

        var handler = new DownloadPurchaseReceptionXmlHandler(
            repo.Object,
            provider.Object,
            detailProcessor.Object,
            tenant.Object,
            company.Object,
            user.Object,
            logger.Object
        );

        return (handler, repo, provider, detailProcessor);
    }

    [Fact]
    public async Task Handle_downloads_and_persists_xml_for_an_existing_imported_document()
    {
        var document = SampleDocument();
        var (handler, repo, provider, _) = BuildHandler();

        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        provider
            .Setup(p =>
                p.GetAuthorizedXmlAsync(
                    TenantId,
                    CompanyId,
                    document.AccessKey,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new SriReceptionXmlQueryResult(
                    Authorized: true,
                    AuthorizationNumber: document.AccessKey,
                    AuthorizationDate: new DateTime(2026, 7, 1, 21, 7, 0, DateTimeKind.Utc),
                    XmlContent: "<factura>...</factura>",
                    ErrorMessage: null
                )
            );

        var result = await handler.Handle(
            new DownloadPurchaseReceptionXmlCommand(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.XmlDownloaded.Should().BeTrue();
        result.Value.Status.Should().Be("VERIFIED");
        result.Value.AuthorizationNumber.Should().Be(document.AccessKey);
        result.Value.DocumentId.Should().Be(document.Id);

        document.Status.Should().Be(PurchaseReceptionDocumentStatus.Verified);
        document.XmlContent.Should().Be("<factura>...</factura>");
        // El mock por defecto no interpreta detalle (Failed) -> nunca un "éxito" silencioso con 0
        // líneas (Fase 5) — se persiste explícitamente como Failed.
        document.ProcessingStatus.Should().Be(PurchaseReceptionProcessingStatus.Failed);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_persists_lines_and_processing_outcome_reported_by_the_detail_processor()
    {
        var supplierId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var document = SampleDocument(supplierId);
        var (handler, repo, provider, detailProcessor) = BuildHandler();

        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        provider
            .Setup(p =>
                p.GetAuthorizedXmlAsync(
                    TenantId,
                    CompanyId,
                    document.AccessKey,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new SriReceptionXmlQueryResult(
                    Authorized: true,
                    AuthorizationNumber: document.AccessKey,
                    AuthorizationDate: DateTime.UtcNow,
                    XmlContent: "<factura>...</factura>",
                    ErrorMessage: null
                )
            );

        var line = PurchaseReceptionLine.Create(
            document.Id,
            TenantId,
            "COCA COLA 500 ML",
            10m,
            0.5m,
            vatCode: "2",
            taxCode: "2",
            vatPercentage: 15m,
            taxValue: 0.75m,
            discountPct: 0m,
            discount: 0m,
            lineSubtotal: 5m,
            totalLine: 5.75m,
            supplierCode: "PROV-001",
            itemId: itemId,
            matchStatus: ItemMatchStatus.AutoMatched
        );
        detailProcessor
            .Setup(p =>
                p.ProcessAsync(
                    document.Id,
                    TenantId,
                    supplierId,
                    "<factura>...</factura>",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new PurchaseReceptionDetailProcessingResult(
                    [line],
                    new PurchaseReceptionProcessingOutcome(
                        PurchaseReceptionProcessingStatus.Processed,
                        1,
                        1,
                        null
                    ),
                    "01",
                    "20"
                )
            );

        var result = await handler.Handle(
            new DownloadPurchaseReceptionXmlCommand(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        document.Lines.Should().ContainSingle();
        var persistedLine = document.Lines.Single();
        persistedLine.ItemId.Should().Be(itemId);
        persistedLine.MatchStatus.Should().Be(ItemMatchStatus.AutoMatched);
        document.ProcessingStatus.Should().Be(PurchaseReceptionProcessingStatus.Processed);
        document.DocTypeCode.Should().Be("01");
        document.SriPaymentMethodCode.Should().Be("20");
    }

    [Fact]
    public async Task Handle_returns_not_found_for_a_nonexistent_document()
    {
        var (handler, repo, provider, _) = BuildHandler();
        var missingId = Guid.NewGuid();
        repo.Setup(r => r.GetByIdAsync(TenantId, missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseReceptionDocument?)null);

        var result = await handler.Handle(
            new DownloadPurchaseReceptionXmlCommand(missingId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        provider.Verify(
            p =>
                p.GetAuthorizedXmlAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Handle_keeps_previous_status_when_sri_query_fails()
    {
        var document = SampleDocument();
        var (handler, repo, provider, _) = BuildHandler();

        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        provider
            .Setup(p =>
                p.GetAuthorizedXmlAsync(
                    TenantId,
                    CompanyId,
                    document.AccessKey,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new SriReceptionXmlQueryResult(
                    Authorized: false,
                    AuthorizationNumber: null,
                    AuthorizationDate: null,
                    XmlContent: null,
                    ErrorMessage: "ERROR_CONEXION"
                )
            );

        var result = await handler.Handle(
            new DownloadPurchaseReceptionXmlCommand(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.SriCommunicationError);

        document.Status.Should().Be(PurchaseReceptionDocumentStatus.Imported);
        document.XmlContent.Should().BeNull();
        document.AuthorizationNumber.Should().BeNull();
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_rejects_a_document_that_is_not_in_Imported_status()
    {
        var document = SampleDocument();
        document.AttachSriAuthorization(
            document.AccessKey,
            DateTime.UtcNow,
            "<factura/>",
            DateTime.UtcNow,
            [],
            UserId,
            docTypeCode: "01",
            sriPaymentMethodCode: "20",
            processing: new PurchaseReceptionProcessingOutcome(
                PurchaseReceptionProcessingStatus.Processed,
                1,
                1,
                null
            )
        );
        document.Status.Should().Be(PurchaseReceptionDocumentStatus.Verified);

        var (handler, repo, provider, _) = BuildHandler();
        repo.Setup(r => r.GetByIdAsync(TenantId, document.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await handler.Handle(
            new DownloadPurchaseReceptionXmlCommand(document.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        provider.Verify(
            p =>
                p.GetAuthorizedXmlAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }
}
