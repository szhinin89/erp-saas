using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Expenses.Events;
using FluentAssertions;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// ACCOUNTING-CONTINUITY-01 — la confirmación de gasto registra una obligación durable Pending cuyo
/// payload conserva subcategoría + importe por línea (nunca una cuenta contable).
/// </summary>
public sealed class ExpenseDocumentConfirmedPostingTranslatorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();

    [Fact]
    public async Task Registra_obligacion_con_una_linea_por_subcategoria_e_importe()
    {
        var h = new AccountingContinuityTestHarness(TenantId, CompanyId);
        var documentId = Guid.NewGuid();
        var lineA = new ExpenseDocumentConfirmedLineAllocation(Guid.NewGuid(), Guid.NewGuid(), 100m, "Internet");
        var lineB = new ExpenseDocumentConfirmedLineAllocation(Guid.NewGuid(), Guid.NewGuid(), 50m, "Suministros");
        var accountingDate = new DateOnly(2026, 9, 3);

        await new ExpenseDocumentConfirmedPostingTranslator(h.Recorder).Handle(
            new ExpenseDocumentConfirmedEvent(
                TenantId,
                documentId,
                Guid.NewGuid(),
                "001-001-000000010",
                CompanyId,
                accountingDate,
                15m,
                165m,
                [lineA, lineB]
            ),
            CancellationToken.None
        );

        var posting = h.Added.Single();
        posting.Status.Should().Be(AccountingPostingStatus.Pending);
        posting.SourceModule.Should().Be("Expenses");
        posting.FactType.Should().Be("DocumentConfirmed");
        posting.SourceEventId.Should().Be(documentId);
        posting.EntryDate.Should().Be(accountingDate, "el gasto contabiliza en su AccountingDate");
        var payload = h.SinglePayload();
        payload.Subtotal.Should().Be(150m);
        payload.TotalVat.Should().Be(15m);
        payload.GrandTotal.Should().Be(165m);
        payload.ExpenseLines.Should().HaveCount(2);
        payload
            .ExpenseLines.Should()
            .Contain(l =>
                l.ExpenseLineId == lineA.ExpenseLineId
                && l.ExpenseSubcategoryId == lineA.ExpenseSubcategoryId
                && l.Amount == 100m
            );
        payload
            .ExpenseLines.Should()
            .Contain(l => l.ExpenseSubcategoryId == lineB.ExpenseSubcategoryId && l.Amount == 50m);
        posting.Payload.Should().NotContain("accountingAccountId");
    }
}
