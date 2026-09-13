using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Application.Modules.Sales.Exceptions;
using ERP.Domain.Modules.Sales.Events;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

public sealed class SalesInvoiceAuthorizedPostingTranslatorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CashSessionId = Guid.NewGuid();

    private static SalesInvoiceAuthorizedEvent Event(
        DateOnly? issueDate = null,
        Guid? invoiceId = null,
        decimal totalIrbpnr = 0m
    ) =>
        new(
            invoiceId ?? Guid.NewGuid(),
            "001-001-000000001",
            115m,
            UserId,
            CashSessionId,
            TenantId,
            CompanyId,
            issueDate ?? new DateOnly(2026, 7, 25),
            100m,
            15m,
            0m,
            0m,
            totalIrbpnr
        );

    private sealed class Mocks
    {
        public Mock<IPostingEngine> PostingEngine { get; } = new();

        public SalesInvoiceAuthorizedPostingTranslator BuildTranslator() =>
            new(PostingEngine.Object);
    }

    [Fact]
    public async Task Evento_valido_construye_PostingFact_correcto()
    {
        var m = new Mocks();
        var invoiceId = Guid.NewGuid();
        var issueDate = new DateOnly(2026, 7, 20);
        PostingFact? captured = null;

        m.PostingEngine.Setup(e =>
                e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>())
            )
            .Callback<PostingFact, CancellationToken>((fact, _) => captured = fact)
            .ReturnsAsync(
                Result<PostingOutcomeDto>.Success(
                    new PostingOutcomeDto(Guid.NewGuid(), PostingOutcomeStatus.Created)
                )
            );

        var translator = m.BuildTranslator();
        await translator.Handle(Event(issueDate, invoiceId), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.TenantId.Should().Be(TenantId);
        captured.CompanyId.Should().Be(CompanyId);
        captured.SourceModule.Should().Be("Sales");
        captured.FactType.Should().Be("InvoiceIssued");
        captured.SourceEventId.Should().Be(invoiceId);
        captured.EntryDate.Should().Be(issueDate);
        captured.Subtotal.Should().Be(100m);
        captured.TotalVat.Should().Be(15m);
        captured.TotalIce.Should().Be(0m);
        captured.TotalDiscount.Should().Be(0m);
        captured.GrandTotal.Should().Be(115m);
        // TAX-LINE-SSOT-ICE-IRBPNR-01 Fase 5E — documento sin IRBPNR no debe generar un
        // TotalIrbpnr falso.
        captured.TotalIrbpnr.Should().Be(0m);
    }

    [Fact]
    public async Task Factura_con_IRBPNR_propaga_TotalIrbpnr_al_PostingFact()
    {
        var m = new Mocks();
        PostingFact? captured = null;
        m.PostingEngine.Setup(e =>
                e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>())
            )
            .Callback<PostingFact, CancellationToken>((fact, _) => captured = fact)
            .ReturnsAsync(
                Result<PostingOutcomeDto>.Success(
                    new PostingOutcomeDto(Guid.NewGuid(), PostingOutcomeStatus.Created)
                )
            );

        var translator = m.BuildTranslator();
        await translator.Handle(Event(totalIrbpnr: 5.10m), CancellationToken.None);

        captured!.TotalIrbpnr.Should().Be(5.10m);
    }

    [Fact]
    public async Task Posting_exitoso_no_lanza_excepcion()
    {
        var m = new Mocks();
        m.PostingEngine.Setup(e =>
                e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                Result<PostingOutcomeDto>.Success(
                    new PostingOutcomeDto(Guid.NewGuid(), PostingOutcomeStatus.Created)
                )
            );

        var translator = m.BuildTranslator();
        var act = async () => await translator.Handle(Event(), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    // SALES-JOURNAL-ENTRY-SILENT-FAILURE-01 Lote 2 — un fallo de IPostingEngine.PostAsync ya NO
    // se traga en un LogWarning: el asiento Sales/InvoiceIssued es obligatorio, así que el
    // Translator lanza SalesInvoicePostingFailedException para que ErpDbContext.SaveChangesAsync
    // revierta la transacción completa de autorización (ver remarks de la clase y de
    // AuthorizeSalesInvoiceHandler). Antes de este lote, este mismo escenario esperaba
    // NotThrowAsync + warning — comportamiento que produjo el hallazgo real (facturas autorizadas
    // en BD dev sin ningún JournalEntry InvoiceIssued).
    [Fact]
    public async Task Posting_failure_lanza_SalesInvoicePostingFailedException()
    {
        var m = new Mocks();
        m.PostingEngine.Setup(e =>
                e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                Result<PostingOutcomeDto>.ValidationFailure(
                    "No existe una regla de contabilización activa.",
                    "RULE_NOT_FOUND"
                )
            );

        var translator = m.BuildTranslator();
        var act = async () => await translator.Handle(Event(), CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<SalesInvoicePostingFailedException>();
        thrown.Which.Code.Should().Be("RULE_NOT_FOUND");
        thrown.Which.Message.Should().Be("No existe una regla de contabilización activa.");
    }

    [Fact]
    public async Task Evento_con_datos_incompletos_falla_correctamente()
    {
        // El Translator solo mapea — Guid.Empty/fechas límite se propagan tal cual al
        // PostingFact; la validación fail-closed real vive en el Pipeline de Fase 3.1, no aquí.
        var m = new Mocks();
        PostingFact? captured = null;
        m.PostingEngine.Setup(e =>
                e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>())
            )
            .Callback<PostingFact, CancellationToken>((fact, _) => captured = fact)
            .ReturnsAsync(
                Result<PostingOutcomeDto>.ValidationFailure(
                    "Período no encontrado.",
                    "PERIOD_NOT_OPEN"
                )
            );

        var evt = new SalesInvoiceAuthorizedEvent(
            Guid.NewGuid(),
            "001-001-000000009",
            10m,
            UserId,
            CashSessionId,
            TenantId,
            Guid.Empty,
            default,
            10m,
            0m,
            0m,
            0m
        );

        var translator = m.BuildTranslator();
        var act = async () => await translator.Handle(evt, CancellationToken.None);

        await act.Should().ThrowAsync<SalesInvoicePostingFailedException>();
        captured!.CompanyId.Should().Be(Guid.Empty);
    }
}
