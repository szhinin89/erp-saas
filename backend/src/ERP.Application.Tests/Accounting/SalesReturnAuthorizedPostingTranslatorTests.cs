using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Modules.Sales.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// P0-01 Fase 7 — SalesReturnAuthorizedPostingTranslator. Mismo patrón exacto que
/// <see cref="SalesInvoiceAuthorizedPostingTranslatorTests"/>: el translator solo mapea
/// SalesReturnAuthorizedEvent → PostingFact e invoca IPostingEngine, nunca resuelve cuentas ni
/// contiene lógica financiera propia.
/// </summary>
public sealed class SalesReturnAuthorizedPostingTranslatorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SalesInvoiceId = Guid.NewGuid();

    private static SalesReturnAuthorizedEvent Event(
        Guid? salesReturnId = null,
        Guid? companyId = null
    ) =>
        new(
            salesReturnId ?? Guid.NewGuid(),
            SalesInvoiceId,
            "DEV-000001",
            23m,
            UserId,
            TenantId,
            companyId ?? CompanyId,
            20m,
            3m,
            0m,
            0m,
            "Producto en mal estado"
        );

    private sealed class Mocks
    {
        public Mock<IPostingEngine> PostingEngine { get; } = new();
        public Mock<ILogger<SalesReturnAuthorizedPostingTranslator>> Logger { get; } = new();

        public SalesReturnAuthorizedPostingTranslator BuildTranslator() =>
            new(PostingEngine.Object, Logger.Object);

        public void VerifyWarningLogged(Times times) =>
            Logger.Verify(
                l =>
                    l.Log(
                        LogLevel.Warning,
                        It.IsAny<EventId>(),
                        It.IsAny<It.IsAnyType>(),
                        It.IsAny<Exception>(),
                        It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                    ),
                times
            );
    }

    [Fact]
    public async Task Evento_valido_construye_PostingFact_correcto()
    {
        var m = new Mocks();
        var salesReturnId = Guid.NewGuid();
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

        var evt = Event(salesReturnId);
        var translator = m.BuildTranslator();
        await translator.Handle(evt, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.TenantId.Should().Be(TenantId);
        captured.CompanyId.Should().Be(CompanyId);
        captured.SourceModule.Should().Be("Sales");
        captured.FactType.Should().Be("SalesReturn");
        captured.SourceEventId.Should().Be(salesReturnId);
        captured.EntryDate.Should().Be(DateOnly.FromDateTime(evt.OccurredOn));
        captured.Subtotal.Should().Be(20m);
        captured.TotalVat.Should().Be(3m);
        captured.TotalIce.Should().Be(0m);
        captured.TotalDiscount.Should().Be(0m);
        captured.GrandTotal.Should().Be(23m);
    }

    [Fact]
    public async Task Posting_exitoso_no_genera_warning()
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
        m.VerifyWarningLogged(Times.Never());
    }

    [Fact]
    public async Task Posting_failure_genera_warning_y_no_lanza_excepcion()
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

        await act.Should().NotThrowAsync();
        m.VerifyWarningLogged(Times.Once());
    }

    [Fact]
    public async Task Evento_con_datos_incompletos_falla_correctamente()
    {
        // El Translator solo mapea — Guid.Empty se propaga tal cual al PostingFact; la validación
        // fail-closed real vive en el Pipeline (JournalFactory/JournalValidator), no aquí.
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

        var evt = Event(companyId: Guid.Empty);
        var translator = m.BuildTranslator();
        var act = async () => await translator.Handle(evt, CancellationToken.None);

        await act.Should().NotThrowAsync();
        captured!.CompanyId.Should().Be(Guid.Empty);
        m.VerifyWarningLogged(Times.Once());
    }
}
