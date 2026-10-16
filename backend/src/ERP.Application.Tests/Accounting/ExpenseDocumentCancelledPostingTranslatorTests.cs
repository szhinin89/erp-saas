using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Expenses.Events;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

public sealed class ExpenseDocumentCancelledPostingTranslatorTests
{
    private static readonly Guid Tenant = Guid.NewGuid(), Company = Guid.NewGuid(), Id = Guid.NewGuid();
    private static ExpenseDocumentCancelledEvent Event() => new(Tenant, Id, Guid.NewGuid(), "GASTO", Company, "motivo");

    [Fact]
    public async Task Usa_identidad_historica_y_fecha_del_acto_sin_reglas_ni_cuentas()
    {
        var recorder = new Mock<IAccountingReversalRecorder>();
        var evt = Event();
        await new ExpenseDocumentCancelledPostingTranslator(recorder.Object).Handle(evt, CancellationToken.None);
        recorder.Verify(r => r.RecordSourceAsync(Tenant, Company, "Expenses", "DocumentConfirmed", Id,
            It.Is<string>(s => s.Contains("motivo")), evt.OccurredOn, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Gate_de_periodo_se_propaga_y_no_se_olvida_como_warning()
    {
        var recorder = new Mock<IAccountingReversalRecorder>();
        recorder.Setup(r => r.RecordSourceAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainRuleViolationException("Per?odo no abierto"));
        var act = () => new ExpenseDocumentCancelledPostingTranslator(recorder.Object).Handle(Event(), CancellationToken.None);
        await act.Should().ThrowAsync<DomainRuleViolationException>();
    }
}
