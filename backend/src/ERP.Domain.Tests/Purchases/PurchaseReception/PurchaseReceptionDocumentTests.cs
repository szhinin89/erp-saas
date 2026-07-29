using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using FluentAssertions;

namespace ERP.Domain.Tests.Purchases.PurchaseReception;

/// <summary>
/// Reglas del endurecimiento del flujo de Recepción XML: el eje de procesamiento
/// (<see cref="PurchaseReceptionProcessingStatus"/>) es independiente del ciclo de vida fiscal
/// (<see cref="PurchaseReceptionDocumentStatus"/>) — un documento Verified puede tener su detalle
/// en Failed, y eso no debe impedir que el XML quede conservado como evidencia.
/// </summary>
public sealed class PurchaseReceptionDocumentTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static PurchaseReceptionDocument SampleDocument() =>
        PurchaseReceptionDocument.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            PurchaseReceptionSourceDocType.Invoice,
            "1791352688001",
            "QUALA ECUADOR S A",
            supplierId: null,
            "0107202601179135268800120150270001617400016174011",
            "015-027-000161740",
            new DateOnly(2026, 7, 1),
            null,
            15.96m,
            2.4m,
            18.35m,
            UserId
        );

    [Fact]
    public void AttachSriAuthorization_persists_Processed_outcome_with_all_lines_ok()
    {
        var document = SampleDocument();
        var line = PurchaseReceptionLine.Create(
            document.Id,
            document.TenantId,
            "Producto de prueba",
            1m,
            1m,
            vatCode: "2",
            taxCode: "2",
            vatPercentage: 15m,
            taxValue: 0.15m,
            discountPct: 0m,
            discount: 0m,
            lineSubtotal: 1m,
            totalLine: 1.15m
        );

        document.AttachSriAuthorization(
            "AUTH-1",
            DateTime.UtcNow,
            "<factura/>",
            DateTime.UtcNow,
            [line],
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
        document.ProcessingStatus.Should().Be(PurchaseReceptionProcessingStatus.Processed);
        document.LinesDetectedCount.Should().Be(1);
        document.LinesProcessedCount.Should().Be(1);
        document.ProcessingNotes.Should().BeNull();
    }

    [Fact]
    public void AttachSriAuthorization_persists_ProcessedWithWarnings_outcome_with_notes()
    {
        var document = SampleDocument();
        var line = PurchaseReceptionLine.Create(
            document.Id,
            document.TenantId,
            "Producto de prueba",
            1m,
            1m,
            vatCode: "2",
            taxCode: "2",
            vatPercentage: 15m,
            taxValue: 0.15m,
            discountPct: 0m,
            discount: 0m,
            lineSubtotal: 1m,
            totalLine: 1.15m
        );

        document.AttachSriAuthorization(
            "AUTH-1",
            DateTime.UtcNow,
            "<factura/>",
            DateTime.UtcNow,
            [line],
            UserId,
            docTypeCode: "01",
            sriPaymentMethodCode: "20",
            processing: new PurchaseReceptionProcessingOutcome(
                PurchaseReceptionProcessingStatus.ProcessedWithWarnings,
                2,
                1,
                "Línea 2: sin IVA — omitida."
            )
        );

        document
            .ProcessingStatus.Should()
            .Be(PurchaseReceptionProcessingStatus.ProcessedWithWarnings);
        document.LinesDetectedCount.Should().Be(2);
        document.LinesProcessedCount.Should().Be(1);
        document.ProcessingNotes.Should().Be("Línea 2: sin IVA — omitida.");
    }

    [Fact]
    public void AttachSriAuthorization_keeps_the_document_Verified_even_when_processing_Failed()
    {
        var document = SampleDocument();

        document.AttachSriAuthorization(
            "AUTH-1",
            DateTime.UtcNow,
            "<factura>ilegible</factura>",
            DateTime.UtcNow,
            [],
            UserId,
            docTypeCode: null,
            sriPaymentMethodCode: null,
            processing: PurchaseReceptionProcessingOutcome.Failed(
                "El XML no tiene un elemento raíz."
            )
        );

        // El comprobante es fiscalmente válido (autorizado por el SRI, XML conservado) aunque el
        // detalle no se haya podido interpretar — los dos ejes de estado son independientes.
        document.Status.Should().Be(PurchaseReceptionDocumentStatus.Verified);
        document.XmlContent.Should().Be("<factura>ilegible</factura>");
        document.ProcessingStatus.Should().Be(PurchaseReceptionProcessingStatus.Failed);
        document.LinesProcessedCount.Should().Be(0);
        document.Lines.Should().BeEmpty();
    }

    [Fact]
    public void AttachSriAuthorization_rejects_a_Failed_outcome_that_reports_processed_lines()
    {
        var document = SampleDocument();
        var line = PurchaseReceptionLine.Create(
            document.Id,
            document.TenantId,
            "Producto de prueba",
            1m,
            1m,
            vatCode: "2",
            taxCode: "2",
            vatPercentage: 15m,
            taxValue: 0.15m,
            discountPct: 0m,
            discount: 0m,
            lineSubtotal: 1m,
            totalLine: 1.15m
        );

        var act = () =>
            document.AttachSriAuthorization(
                "AUTH-1",
                DateTime.UtcNow,
                "<factura/>",
                DateTime.UtcNow,
                [line],
                UserId,
                docTypeCode: "01",
                sriPaymentMethodCode: "20",
                processing: new PurchaseReceptionProcessingOutcome(
                    PurchaseReceptionProcessingStatus.Failed,
                    1,
                    1,
                    null
                )
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AttachSriAuthorization_rejects_a_Processed_outcome_without_any_processed_line()
    {
        var document = SampleDocument();

        var act = () =>
            document.AttachSriAuthorization(
                "AUTH-1",
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
                    0,
                    null
                )
            );

        act.Should().Throw<ArgumentException>();
    }

    private static PurchaseReceptionDocument FailedVerifiedDocument()
    {
        var document = SampleDocument();
        document.AttachSriAuthorization(
            "AUTH-1",
            DateTime.UtcNow,
            "<factura>ilegible</factura>",
            DateTime.UtcNow,
            [],
            UserId,
            docTypeCode: null,
            sriPaymentMethodCode: null,
            processing: PurchaseReceptionProcessingOutcome.Failed(
                "El XML no tiene un elemento raíz."
            )
        );
        return document;
    }

    [Fact]
    public void ReprocessDetail_replaces_lines_and_updates_processing_when_previous_attempt_failed()
    {
        var document = FailedVerifiedDocument();
        var line = PurchaseReceptionLine.Create(
            document.Id,
            document.TenantId,
            "Producto reprocesado",
            1m,
            1m,
            vatCode: "2",
            taxCode: "2",
            vatPercentage: 15m,
            taxValue: 0.15m,
            discountPct: 0m,
            discount: 0m,
            lineSubtotal: 1m,
            totalLine: 1.15m
        );

        document.ReprocessDetail(
            [line],
            new PurchaseReceptionProcessingOutcome(
                PurchaseReceptionProcessingStatus.Processed,
                1,
                1,
                null
            ),
            docTypeCode: "01",
            sriPaymentMethodCode: "20",
            updatedBy: UserId
        );

        document.ProcessingStatus.Should().Be(PurchaseReceptionProcessingStatus.Processed);
        document.Lines.Should().ContainSingle();
        document.DocTypeCode.Should().Be("01");
        document.SriPaymentMethodCode.Should().Be("20");
        // El comprobante ya era fiscalmente válido (Verified) desde la descarga original —
        // reprocesar el detalle no vuelve a tocar esa evidencia.
        document.Status.Should().Be(PurchaseReceptionDocumentStatus.Verified);
        document.XmlContent.Should().Be("<factura>ilegible</factura>");
    }

    [Fact]
    public void ReprocessDetail_keeps_existing_header_values_when_the_new_attempt_cannot_read_them()
    {
        var document = SampleDocument();
        document.AttachSriAuthorization(
            "AUTH-1",
            DateTime.UtcNow,
            "<factura>...</factura>",
            DateTime.UtcNow,
            [],
            UserId,
            docTypeCode: "01",
            sriPaymentMethodCode: "20",
            processing: PurchaseReceptionProcessingOutcome.Failed(
                "Ninguna línea tiene impuesto IVA."
            )
        );
        var line = PurchaseReceptionLine.Create(
            document.Id,
            document.TenantId,
            "Producto reprocesado",
            1m,
            1m,
            vatCode: "2",
            taxCode: "2",
            vatPercentage: 15m,
            taxValue: 0.15m,
            discountPct: 0m,
            discount: 0m,
            lineSubtotal: 1m,
            totalLine: 1.15m
        );

        // Un reprocesamiento que igual no logra leer la cabecera (null) no debe borrar el
        // docTypeCode/sriPaymentMethodCode ya guardados de la descarga original.
        document.ReprocessDetail(
            [line],
            new PurchaseReceptionProcessingOutcome(
                PurchaseReceptionProcessingStatus.Processed,
                1,
                1,
                null
            ),
            docTypeCode: null,
            sriPaymentMethodCode: null,
            updatedBy: UserId
        );

        document.DocTypeCode.Should().Be("01");
        document.SriPaymentMethodCode.Should().Be("20");
    }

    [Fact]
    public void ReprocessDetail_rejects_a_document_whose_previous_processing_already_succeeded()
    {
        var document = SampleDocument();
        var existingLine = PurchaseReceptionLine.Create(
            document.Id,
            document.TenantId,
            "Producto ya conciliado",
            1m,
            1m,
            vatCode: "2",
            taxCode: "2",
            vatPercentage: 15m,
            taxValue: 0.15m,
            discountPct: 0m,
            discount: 0m,
            lineSubtotal: 1m,
            totalLine: 1.15m,
            itemId: Guid.NewGuid(),
            matchStatus: ItemMatchStatus.ManuallyMatched
        );
        document.AttachSriAuthorization(
            "AUTH-1",
            DateTime.UtcNow,
            "<factura/>",
            DateTime.UtcNow,
            [existingLine],
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

        // No debe existir forma de pisar un Item Matching ya resuelto (ManuallyMatched) mediante
        // un reprocesamiento — la guarda EnsureFailed protege ese trabajo del usuario.
        var act = () =>
            document.ReprocessDetail(
                [],
                PurchaseReceptionProcessingOutcome.Failed("intento espurio"),
                docTypeCode: null,
                sriPaymentMethodCode: null,
                updatedBy: UserId
            );

        act.Should().Throw<InvalidOperationException>();
        document.Lines.Should().ContainSingle();
        document.Lines[0].MatchStatus.Should().Be(ItemMatchStatus.ManuallyMatched);
    }

    [Fact]
    public void ReprocessDetail_rejects_a_document_that_is_not_Verified()
    {
        var document = SampleDocument();

        var act = () =>
            document.ReprocessDetail(
                [],
                PurchaseReceptionProcessingOutcome.Failed("sin XML"),
                docTypeCode: null,
                sriPaymentMethodCode: null,
                updatedBy: UserId
            );

        act.Should().Throw<InvalidOperationException>();
    }
}
