using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Modules.Purchases.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// P0-02 Fase 6 — PurchaseReturnAuthorizedPostingTranslator. Mismo patrón exacto que
/// <see cref="SalesReturnAuthorizedPostingTranslatorTests"/>: el translator solo mapea
/// PurchaseReturnAuthorizedEvent → PostingFact (usando los 5 campos agregados en la Remediación 01)
/// e invoca IPostingEngine, nunca resuelve cuentas ni contiene lógica financiera propia.
/// </summary>
public sealed class PurchaseReturnAuthorizedPostingTranslatorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid PurchaseInvoiceId = Guid.NewGuid();

    private static PurchaseReturnAuthorizedEvent Event(
        Guid? purchaseReturnId = null,
        Guid? companyId = null,
        decimal historicalCostTotal = 350m,
        decimal costVarianceTotal = 31m,
        decimal appliedToPayableAmount = 381m,
        decimal supplierCreditAmount = 0m,
        Guid? supplierCreditId = null,
        decimal authorizedIrbpnrTotal = 0m
    ) =>
        new(
            purchaseReturnId ?? Guid.NewGuid(),
            PurchaseInvoiceId,
            SupplierId,
            BranchId,
            TenantId,
            companyId ?? CompanyId,
            "DEV-000001",
            UserId,
            authorizedSubtotal: 350m,
            authorizedVatTotal: 31m,
            authorizedIceTotal: 0m,
            authorizedDiscountTotal: 0m,
            grandTotal: 381m,
            historicalCostTotal: historicalCostTotal,
            costVarianceTotal: costVarianceTotal,
            appliedToPayableAmount: appliedToPayableAmount,
            supplierCreditAmount: supplierCreditAmount,
            supplierCreditId: supplierCreditId,
            reason: "Producto defectuoso",
            authorizedIrbpnrTotal: authorizedIrbpnrTotal
        );

    private sealed class Mocks
    {
        public Mock<IPostingEngine> PostingEngine { get; } = new();
        public Mock<ILogger<PurchaseReturnAuthorizedPostingTranslator>> Logger { get; } = new();

        public PurchaseReturnAuthorizedPostingTranslator BuildTranslator() =>
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
    public async Task Evento_valido_construye_PostingFact_correcto_con_variance_positiva()
    {
        var m = new Mocks();
        var purchaseReturnId = Guid.NewGuid();
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

        var evt = Event(purchaseReturnId, costVarianceTotal: 31m);
        var translator = m.BuildTranslator();
        await translator.Handle(evt, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.TenantId.Should().Be(TenantId);
        captured.CompanyId.Should().Be(CompanyId);
        captured.SourceModule.Should().Be("Purchases");
        captured.FactType.Should().Be("PurchaseReturn");
        captured.SourceEventId.Should().Be(purchaseReturnId);
        captured.EntryDate.Should().Be(DateOnly.FromDateTime(evt.OccurredOn));
        captured.TotalVat.Should().Be(31m);
        captured.TotalIce.Should().Be(0m);
        captured.AppliedToPayableAmount.Should().Be(381m);
        captured.SupplierCreditAmount.Should().Be(0m);
        captured.CostVarianceDebitAmount.Should().Be(31m);
        captured.CostVarianceCreditAmount.Should().Be(0m);
        captured.HistoricalCostTotal.Should().Be(350m);
        // TAX-LINE-SSOT-ICE-IRBPNR-01 Fase 5E — documento sin IRBPNR no debe generar un
        // TotalIrbpnr falso.
        captured.TotalIrbpnr.Should().Be(0m);
    }

    [Fact]
    public async Task Devolucion_con_IRBPNR_propaga_TotalIrbpnr_al_PostingFact()
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

        var evt = Event(authorizedIrbpnrTotal: 6.30m);
        var translator = m.BuildTranslator();
        await translator.Handle(evt, CancellationToken.None);

        captured!.TotalIrbpnr.Should().Be(6.30m);
    }

    [Fact]
    public async Task CostVarianceTotal_negativo_mapea_solo_al_credito_nunca_al_debito()
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

        var evt = Event(costVarianceTotal: -12m);
        var translator = m.BuildTranslator();
        await translator.Handle(evt, CancellationToken.None);

        captured!.CostVarianceDebitAmount.Should().Be(0m);
        captured.CostVarianceCreditAmount.Should().Be(12m);
    }

    [Fact]
    public async Task CostVarianceTotal_cero_no_activa_ninguna_linea_condicional()
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

        var evt = Event(costVarianceTotal: 0m);
        var translator = m.BuildTranslator();
        await translator.Handle(evt, CancellationToken.None);

        captured!.CostVarianceDebitAmount.Should().Be(0m);
        captured.CostVarianceCreditAmount.Should().Be(0m);
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
