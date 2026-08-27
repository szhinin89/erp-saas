using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Application.Modules.Expenses.Exceptions;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Expenses.Events;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// EXPENSES-CONFIRM-07 — a diferencia de <see cref="PurchaseInvoiceConfirmedPostingTranslatorTests"/>,
/// un posting fallido aqui debe LANZAR (nunca solo loguear), para que
/// <c>ErpDbContext.SaveChangesAsync</c> revierta toda la confirmacion. Ver
/// <see cref="ExpensePostingFailedException"/>.
/// </summary>
public sealed class ExpenseDocumentConfirmedPostingTranslatorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();

    private static ExpenseDocumentConfirmedEvent Event(
        Guid? documentId = null,
        IReadOnlyList<ExpenseDocumentConfirmedLineAllocation>? allocations = null
    ) =>
        new(
            TenantId,
            documentId ?? Guid.NewGuid(),
            SupplierId,
            "001-001-000000001",
            CompanyId,
            new DateOnly(2026, 8, 27),
            15m,
            115m,
            allocations
                ?? new[]
                {
                    new ExpenseDocumentConfirmedLineAllocation(Guid.NewGuid(), Guid.NewGuid(), 100m, "Internet"),
                }
        );

    private sealed class Mocks
    {
        public Mock<IPostingEngine> PostingEngine { get; } = new();

        public ExpenseDocumentConfirmedPostingTranslator BuildTranslator() => new(PostingEngine.Object);
    }

    [Fact]
    public async Task Evento_valido_construye_PostingFact_con_una_allocation_por_linea()
    {
        var m = new Mocks();
        var lineId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        PostingFact? captured = null;
        m.PostingEngine
            .Setup(e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()))
            .Callback<PostingFact, CancellationToken>((fact, _) => captured = fact)
            .ReturnsAsync(Result<PostingOutcomeDto>.Success(new PostingOutcomeDto(Guid.NewGuid(), PostingOutcomeStatus.Created)));

        var evt = Event(
            documentId,
            new[] { new ExpenseDocumentConfirmedLineAllocation(lineId, accountId, 100m, "Internet") }
        );

        await m.BuildTranslator().Handle(evt, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.TenantId.Should().Be(TenantId);
        captured.CompanyId.Should().Be(CompanyId);
        captured.SourceModule.Should().Be("Expenses");
        captured.FactType.Should().Be("DocumentConfirmed");
        captured.SourceEventId.Should().Be(documentId);
        captured.TotalVat.Should().Be(15m);
        captured.GrandTotal.Should().Be(115m);
        captured.Allocations.Should().ContainSingle();
        var allocation = captured.Allocations!.Single();
        allocation.AccountingAccountId.Should().Be(accountId);
        allocation.Amount.Should().Be(100m);
        allocation.Nature.Should().Be(AccountNature.Debit);
        allocation.SourceLineId.Should().Be(lineId);
    }

    [Fact]
    public async Task Evento_con_tres_lineas_genera_tres_allocations()
    {
        var m = new Mocks();
        PostingFact? captured = null;
        m.PostingEngine
            .Setup(e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()))
            .Callback<PostingFact, CancellationToken>((fact, _) => captured = fact)
            .ReturnsAsync(Result<PostingOutcomeDto>.Success(new PostingOutcomeDto(Guid.NewGuid(), PostingOutcomeStatus.Created)));

        var allocations = new[]
        {
            new ExpenseDocumentConfirmedLineAllocation(Guid.NewGuid(), Guid.NewGuid(), 50m, "A"),
            new ExpenseDocumentConfirmedLineAllocation(Guid.NewGuid(), Guid.NewGuid(), 40m, "B"),
            new ExpenseDocumentConfirmedLineAllocation(Guid.NewGuid(), Guid.NewGuid(), 25m, "C"),
        };

        await m.BuildTranslator().Handle(Event(allocations: allocations), CancellationToken.None);

        captured!.Allocations.Should().HaveCount(3);
    }

    [Fact]
    public async Task Posting_exitoso_no_lanza()
    {
        var m = new Mocks();
        m.PostingEngine
            .Setup(e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PostingOutcomeDto>.Success(new PostingOutcomeDto(Guid.NewGuid(), PostingOutcomeStatus.Created)));

        var act = async () => await m.BuildTranslator().Handle(Event(), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Posting_failure_lanza_ExpensePostingFailedException_en_vez_de_loguear_warning()
    {
        var m = new Mocks();
        m.PostingEngine
            .Setup(e => e.PostAsync(It.IsAny<PostingFact>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PostingOutcomeDto>.ValidationFailure("No existe regla de contabilizacion.", "RULE_NOT_FOUND"));

        var act = async () => await m.BuildTranslator().Handle(Event(), CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<ExpensePostingFailedException>();
        thrown.Which.Code.Should().Be("RULE_NOT_FOUND");
        thrown.Which.Message.Should().Be("No existe regla de contabilizacion.");
    }
}
