using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Modules.Finance.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>Fase 5.6.3 — SupplierPaymentAppliedPostingTranslator (Fase 5.6.1).</summary>
public sealed class SupplierPaymentAppliedPostingTranslatorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();

    private static SupplierPaymentAppliedEvent Event(
        DateOnly? paymentDate = null,
        Guid? paymentId = null
    ) =>
        new(
            TenantId,
            paymentId ?? Guid.NewGuid(),
            CompanyId,
            SupplierId,
            250m,
            paymentDate ?? new DateOnly(2026, 7, 25)
        );

    private sealed class Mocks
    {
        public Mock<IPostingEngine> PostingEngine { get; } = new();
        public Mock<ILogger<SupplierPaymentAppliedPostingTranslator>> Logger { get; } = new();

        public SupplierPaymentAppliedPostingTranslator BuildTranslator() =>
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
        var paymentId = Guid.NewGuid();
        var paymentDate = new DateOnly(2026, 7, 20);
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
        await translator.Handle(Event(paymentDate, paymentId), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.TenantId.Should().Be(TenantId);
        captured.CompanyId.Should().Be(CompanyId);
        captured.SourceModule.Should().Be("Finance");
        captured.FactType.Should().Be("SupplierPaymentApplied");
        captured.SourceEventId.Should().Be(paymentId);
        captured.EntryDate.Should().Be(paymentDate);
        captured.GrandTotal.Should().Be(250m);
        captured.Subtotal.Should().Be(0m);
        captured.TotalVat.Should().Be(0m);
        captured.TotalIce.Should().Be(0m);
        captured.TotalDiscount.Should().Be(0m);
    }

    [Fact]
    public async Task Handle_invoca_PostAsync_exactamente_una_vez()
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
        await translator.Handle(Event(), CancellationToken.None);

        m.PostingEngine.Verify(
            e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
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
}
