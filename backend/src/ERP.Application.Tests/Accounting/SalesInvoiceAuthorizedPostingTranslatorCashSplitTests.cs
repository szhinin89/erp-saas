using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Modules.Sales.Events;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// SALES-CASH-VS-RECEIVABLE-POSTING-SPLIT-AND-CANCEL-REVERSAL-01 Lote 3 — confirma que
/// <see cref="SalesInvoiceAuthorizedPostingTranslator"/> deriva <c>PostingFact.CashApplied</c>/
/// <c>PendingBalance</c> desde <c>SalesInvoiceAuthorizedEvent.CashApplied</c>/<c>GrandTotal</c>,
/// mismo cálculo que <c>SalesSettlementPolicy.Calculate</c> (dominio): PendingBalance = GrandTotal
/// - CashApplied, nunca negativo. Archivo separado de
/// <c>SalesInvoiceAuthorizedPostingTranslatorTests.cs</c> (Lote 1/2, no tocar) para no interferir
/// con esa suite ya cerrada.
/// </summary>
public sealed class SalesInvoiceAuthorizedPostingTranslatorCashSplitTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CashSessionId = Guid.NewGuid();

    private static SalesInvoiceAuthorizedEvent Event(decimal grandTotal, decimal cashApplied) =>
        new(
            Guid.NewGuid(),
            "001-001-000000001",
            grandTotal,
            UserId,
            CashSessionId,
            TenantId,
            CompanyId,
            new DateOnly(2026, 9, 13),
            grandTotal,
            0m,
            0m,
            0m,
            totalIrbpnr: 0m,
            cashApplied: cashApplied
        );

    private static async Task<PostingFact> CaptureFactAsync(SalesInvoiceAuthorizedEvent evt)
    {
        var postingEngine = new Mock<IPostingEngine>();
        PostingFact? captured = null;
        postingEngine
            .Setup(e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()))
            .Callback<PostingFact, CancellationToken>((fact, _) => captured = fact)
            .ReturnsAsync(
                Result<PostingOutcomeDto>.Success(
                    new PostingOutcomeDto(Guid.NewGuid(), PostingOutcomeStatus.Created)
                )
            );

        var translator = new SalesInvoiceAuthorizedPostingTranslator(postingEngine.Object);
        await translator.Handle(evt, CancellationToken.None);

        return captured!;
    }

    [Fact]
    public async Task Venta_contado_CashApplied_igual_a_GrandTotal_PendingBalance_cero()
    {
        var fact = await CaptureFactAsync(Event(grandTotal: 115m, cashApplied: 115m));

        fact.CashApplied.Should().Be(115m);
        fact.PendingBalance.Should().Be(0m);
    }

    [Fact]
    public async Task Venta_credito_puro_CashApplied_cero_PendingBalance_igual_a_GrandTotal()
    {
        var fact = await CaptureFactAsync(Event(grandTotal: 115m, cashApplied: 0m));

        fact.CashApplied.Should().Be(0m);
        fact.PendingBalance.Should().Be(115m);
    }

    [Fact]
    public async Task Venta_parcial_CashApplied_y_PendingBalance_suman_GrandTotal()
    {
        var fact = await CaptureFactAsync(Event(grandTotal: 115m, cashApplied: 40m));

        fact.CashApplied.Should().Be(40m);
        fact.PendingBalance.Should().Be(75m);
        (fact.CashApplied!.Value + fact.PendingBalance!.Value).Should().Be(115m);
    }
}
