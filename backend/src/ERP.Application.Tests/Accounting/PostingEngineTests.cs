using ERP.Application.Modules.Accounting.Posting;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

public sealed class PostingEngineTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();

    private static PostingFact Fact(DateOnly? entryDate = null) => new(
        TenantId, CompanyId, "Sales", "InvoiceIssued", Guid.NewGuid(), entryDate ?? new DateOnly(2026, 7, 15),
        100m, 15m, 0m, 0m, 115m);

    private static AccountingPeriod OpenPeriod() => AccountingPeriod.Create(
        TenantId, CompanyId, 2026, 7, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), CreatedBy);

    private static AccountingPeriod ClosedPeriod()
    {
        var period = OpenPeriod();
        period.Close(CreatedBy);
        return period;
    }

    // Fase 3.5.5: la regla ahora necesita PostingRuleLine para que JournalFactory genere líneas
    // reales — Débito GrandTotal (115) == Crédito Subtotal (100) + Crédito TaxVat (15), balanceado
    // con los montos fijos de Fact().
    private static PostingRule Rule()
    {
        var rule = PostingRule.Create(
            TenantId, CompanyId, "Sales", "InvoiceIssued", Guid.NewGuid(), Guid.NewGuid(), null, CreatedBy);
        rule.AddLine(Guid.NewGuid(), AccountNature.Debit, PostingAmountKind.GrandTotal);
        rule.AddLine(Guid.NewGuid(), AccountNature.Credit, PostingAmountKind.Subtotal);
        rule.AddLine(Guid.NewGuid(), AccountNature.Credit, PostingAmountKind.TaxVat);
        return rule;
    }

    private sealed class Mocks
    {
        public Mock<IJournalEntryRepository> JournalEntries { get; } = new();
        public Mock<IPostingRuleRepository> PostingRules { get; } = new();
        public Mock<IAccountingPeriodRepository> AccountingPeriods { get; } = new();

        public Mocks()
        {
            // El lock se adquiere siempre antes de FindByKeyAsync (Fase 3.3.5) — Setup por
            // defecto en todos los tests; cada test puede sobrescribirlo si necesita otro
            // comportamiento.
            JournalEntries
                .Setup(r => r.AcquireIdempotencyLockAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        public PostingEngine BuildEngine() => new(
            JournalEntries.Object, PostingRules.Object, AccountingPeriods.Object);
    }

    [Fact]
    public async Task Sin_regla_de_contabilizacion_retorna_RULE_NOT_FOUND_y_no_persiste()
    {
        var m = new Mocks();
        m.JournalEntries
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Sales", "InvoiceIssued", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JournalEntry?)null);
        m.PostingRules
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Sales", "InvoiceIssued", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostingRule?)null);

        var engine = m.BuildEngine();
        var result = await engine.PostAsync(Fact());

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be("RULE_NOT_FOUND");
        m.JournalEntries.Verify(r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Periodo_no_abierto_retorna_PERIOD_NOT_OPEN_y_no_persiste()
    {
        var m = new Mocks();
        m.JournalEntries
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Sales", "InvoiceIssued", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JournalEntry?)null);
        m.PostingRules
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Sales", "InvoiceIssued", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Rule());
        m.AccountingPeriods
            .Setup(r => r.FindContainingDateAsync(TenantId, CompanyId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClosedPeriod());

        var engine = m.BuildEngine();
        var result = await engine.PostAsync(Fact());

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be("PERIOD_NOT_OPEN");
        m.JournalEntries.Verify(r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Hecho_nuevo_con_regla_y_periodo_abierto_crea_el_asiento()
    {
        var m = new Mocks();
        m.JournalEntries
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Sales", "InvoiceIssued", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JournalEntry?)null);
        m.PostingRules
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Sales", "InvoiceIssued", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Rule());
        m.AccountingPeriods
            .Setup(r => r.FindContainingDateAsync(TenantId, CompanyId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OpenPeriod());

        var engine = m.BuildEngine();
        var result = await engine.PostAsync(Fact());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(PostingOutcomeStatus.Created);
        m.JournalEntries.Verify(r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()), Times.Once);
        // PostingPipeline prepara (staging) pero nunca comitea — la persistencia pertenece al
        // ciclo externo de ErpDbContext.SaveChangesAsync (Fase 3.3.1/3.3.5).
        m.JournalEntries.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        m.JournalEntries.Verify(
            r => r.AcquireIdempotencyLockAsync(CompanyId, "Sales", It.IsAny<Guid>(), "InvoiceIssued", It.IsAny<CancellationToken>()),
            Times.Once);

        // Orden: el lock se adquiere antes de la lectura de idempotencia.
        var invocations = m.JournalEntries.Invocations.Select(i => i.Method.Name).ToList();
        var lockCall = invocations.FindIndex(name => name == nameof(IJournalEntryRepository.AcquireIdempotencyLockAsync));
        var findCall = invocations.FindIndex(name => name == nameof(IJournalEntryRepository.FindByKeyAsync));
        lockCall.Should().BeLessThan(findCall, because: "el advisory lock debe adquirirse antes de consultar la clave de idempotencia");
    }

    [Fact]
    public async Task Hecho_ya_contabilizado_retorna_AlreadyProcessed_sin_resolver_regla_ni_periodo()
    {
        var existing = JournalEntry.Create(
            TenantId, CompanyId, new DateOnly(2026, 7, 15), Guid.NewGuid(),
            "Sales", "InvoiceIssued", Guid.NewGuid(), "Sales — InvoiceIssued — existing", Guid.Empty);

        var m = new Mocks();
        m.JournalEntries
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Sales", "InvoiceIssued", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var engine = m.BuildEngine();
        var result = await engine.PostAsync(Fact());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(PostingOutcomeStatus.AlreadyProcessed);
        result.Value.JournalEntryId.Should().Be(existing.Id);
        m.PostingRules.Verify(
            r => r.FindByKeyAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        m.AccountingPeriods.Verify(
            r => r.FindContainingDateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()),
            Times.Never);
        m.JournalEntries.Verify(r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
