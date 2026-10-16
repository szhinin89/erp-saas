using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Finance.Events;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

public sealed class CollectionReversedPostingTranslatorTests
{
    private static readonly Guid Tenant = Guid.NewGuid(), Company = Guid.NewGuid(), Id = Guid.NewGuid();
    private static CollectionReversedEvent Event() => new(Tenant, Id, Company, Guid.NewGuid(), 150m, "motivo");

    [Fact]
    public async Task Usa_identidad_historica_y_fecha_del_acto_sin_reglas_ni_cuentas()
    {
        var recorder = new Mock<IAccountingReversalRecorder>();
        var evt = Event();
        await new CollectionReversedPostingTranslator(recorder.Object).Handle(evt, CancellationToken.None);
        recorder.Verify(r => r.RecordSourceAsync(Tenant, Company, "Finance", "CollectionApplied", Id,
            It.Is<string>(s => s.Contains("motivo")), evt.OccurredOn, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Gate_de_periodo_se_propaga_y_no_se_olvida_como_warning()
    {
        var recorder = new Mock<IAccountingReversalRecorder>();
        recorder.Setup(r => r.RecordSourceAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainRuleViolationException("Período no abierto"));
        var act = () => new CollectionReversedPostingTranslator(recorder.Object).Handle(Event(), CancellationToken.None);
        await act.Should().ThrowAsync<DomainRuleViolationException>();
    }
}
