using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Sales.Events;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

public sealed class SalesInvoiceCancelledPostingTranslatorTests
{
    private static readonly Guid Tenant = Guid.NewGuid(), Company = Guid.NewGuid(), Id = Guid.NewGuid();
    private static SalesInvoiceCancelledEvent Event() => new(Tenant, Id, Guid.NewGuid(), "VENTA", 115m, "motivo", Company);

    private static Mock<IAccountingPostingRepository> Postings(params AccountingPosting[] costs)
    {
        var postings = new Mock<IAccountingPostingRepository>();
        postings.Setup(p => p.ListBySourceDocumentAsync(Tenant, Company, Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(costs);
        return postings;
    }

    private static AccountingPosting Cost(string factType, Guid sourceId) => AccountingPosting.CreatePending(
        Tenant, Company, "Sales", factType, sourceId, "VENTA", new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1),
        AccountingPostingPayload.CurrentVersion, "{}", Guid.NewGuid(), null, Id);

    [Fact]
    public async Task Usa_identidad_historica_y_fecha_del_acto_sin_reglas_ni_cuentas()
    {
        var recorder = new Mock<IAccountingReversalRecorder>();
        var evt = Event();
        await new SalesInvoiceCancelledPostingTranslator(recorder.Object, Postings().Object).Handle(evt, CancellationToken.None);
        recorder.Verify(r => r.RecordSourceAsync(Tenant, Company, "Sales", "InvoiceIssued", Id,
            It.Is<string>(s => s.Contains("motivo")), evt.OccurredOn, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// ACCOUNTING-CONTINUITY-05 — cada obligación de costo de la factura (original, cobertura, devolución)
    /// pasa por la anulación común con la fecha efectiva del acto (nunca la fecha original del costo),
    /// una sola vez por identidad.
    /// </summary>
    [Fact]
    public async Task C05_costos_de_la_factura_se_anulan_por_identidad_con_la_fecha_del_acto()
    {
        var coverage = Guid.NewGuid();
        var returned = Guid.NewGuid();
        var recorder = new Mock<IAccountingReversalRecorder>();
        var evt = Event();
        await new SalesInvoiceCancelledPostingTranslator(recorder.Object, Postings(
                Cost("CostOfGoodsSold", Id), Cost("CostOfGoodsSold", coverage), Cost("CostOfGoodsSoldReversed", returned)).Object)
            .Handle(evt, CancellationToken.None);

        foreach (var (fact, source) in new[] { ("CostOfGoodsSold", Id), ("CostOfGoodsSold", coverage), ("CostOfGoodsSoldReversed", returned) })
            recorder.Verify(r => r.RecordSourceAsync(Tenant, Company, "Sales", fact, source,
                It.IsAny<string>(), evt.OccurredOn, It.IsAny<CancellationToken>()), Times.Once);
        recorder.Verify(r => r.RecordSourceAsync(Tenant, Company, "Sales", "InvoiceIssued", Id,
            It.IsAny<string>(), evt.OccurredOn, It.IsAny<CancellationToken>()), Times.Once);
        recorder.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Gate_de_periodo_se_propaga_y_no_se_olvida_como_warning()
    {
        var recorder = new Mock<IAccountingReversalRecorder>();
        recorder.Setup(r => r.RecordSourceAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainRuleViolationException("Período no abierto"));
        var act = () => new SalesInvoiceCancelledPostingTranslator(recorder.Object, Postings().Object).Handle(Event(), CancellationToken.None);
        await act.Should().ThrowAsync<DomainRuleViolationException>();
    }
}
