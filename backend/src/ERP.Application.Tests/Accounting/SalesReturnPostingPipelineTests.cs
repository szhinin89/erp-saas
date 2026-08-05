using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Sales.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// P0-01 Fase 7 — integración SalesReturnAuthorizedEvent → SalesReturnAuthorizedPostingTranslator
/// → IPostingEngine (real, con repositorios mockeados) → JournalEntry balanceado. Mismo patrón que
/// <see cref="PostingEngineTests"/> (Fase 3 de Accounting): ejercita el PostingEngine real —
/// PostingRuleResolver, PostingPeriodResolver/Guard, JournalFactory, JournalValidator,
/// ReserveNextNumberAsync — sin tocar EF/Postgres, cien por ciento dentro de ERP.Application.Tests.
/// </summary>
public sealed class SalesReturnPostingPipelineTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();
    private static readonly Guid SalesInvoiceId = Guid.NewGuid();

    private static SalesReturnAuthorizedEvent Event() =>
        new(
            Guid.NewGuid(),
            SalesInvoiceId,
            "DEV-000001",
            23m,
            CreatedBy,
            TenantId,
            CompanyId,
            20m,
            3m,
            0m,
            0m,
            "Producto en mal estado"
        );

    private static AccountingPeriod OpenPeriod(DateOnly entryDate) =>
        AccountingPeriod.Create(
            TenantId,
            CompanyId,
            entryDate.Year,
            entryDate.Month,
            new DateOnly(entryDate.Year, entryDate.Month, 1),
            new DateOnly(
                entryDate.Year,
                entryDate.Month,
                DateTime.DaysInMonth(entryDate.Year, entryDate.Month)
            ),
            CreatedBy
        );

    // Regla de datos para (Sales, SalesReturn): Débito GrandTotal (23) == Crédito Subtotal (20) +
    // Crédito TaxVat (3) — balanceado con los montos fijos de Event(). El sentido contable real
    // (qué cuentas exactas) es una decisión de configuración fuera de alcance de esta fase; el
    // test solo verifica que el pipeline resuelve la regla, genera y balancea el asiento.
    private static PostingRule Rule()
    {
        var rule = PostingRule.Create(
            TenantId,
            CompanyId,
            "Sales",
            "SalesReturn",
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            CreatedBy
        );
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
        public Mock<IJournalEntrySequenceRepository> JournalEntrySequences { get; } = new();
        public Mock<ILogger<SalesReturnAuthorizedPostingTranslator>> Logger { get; } = new();

        public Mocks()
        {
            JournalEntries
                .Setup(r =>
                    r.AcquireIdempotencyLockAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<string>(),
                        It.IsAny<Guid>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(Task.CompletedTask);

            var nextNumber = 0;
            JournalEntrySequences
                .Setup(r =>
                    r.ReserveNextNumberAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<Guid>(),
                        It.IsAny<int>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(() => ++nextNumber);
        }

        public PostingEngine BuildEngine() =>
            new(
                JournalEntries.Object,
                PostingRules.Object,
                AccountingPeriods.Object,
                JournalEntrySequences.Object
            );

        public SalesReturnAuthorizedPostingTranslator BuildTranslator(IPostingEngine engine) =>
            new(engine, Logger.Object);
    }

    [Fact]
    public async Task Pipeline_completo_genera_JournalEntry_balanceado_para_SalesReturn()
    {
        var evt = Event();
        var entryDate = DateOnly.FromDateTime(evt.OccurredOn);
        var m = new Mocks();

        m.JournalEntries.Setup(r =>
                r.FindByKeyAsync(
                    TenantId,
                    CompanyId,
                    "Sales",
                    "SalesReturn",
                    evt.SalesReturnId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((JournalEntry?)null);
        m.PostingRules.Setup(r =>
                r.FindByKeyAsync(
                    TenantId,
                    CompanyId,
                    "Sales",
                    "SalesReturn",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Rule());
        m.AccountingPeriods.Setup(r =>
                r.FindContainingDateAsync(
                    TenantId,
                    CompanyId,
                    It.IsAny<DateOnly>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(OpenPeriod(entryDate));

        JournalEntry? captured = null;
        m.JournalEntries.Setup(r =>
                r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>())
            )
            .Callback<JournalEntry, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        var engine = m.BuildEngine();
        var translator = m.BuildTranslator(engine);

        await translator.Handle(evt, CancellationToken.None);

        m.JournalEntries.Verify(
            r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        captured.Should().NotBeNull();
        captured!.SourceModule.Should().Be("Sales");
        captured.SourceEventType.Should().Be("SalesReturn");
        captured.SourceEventId.Should().Be(evt.SalesReturnId);
        captured.Status.Should().Be(JournalEntryStatus.Posted);
        captured.PostedAtUtc.Should().NotBeNull();
        captured.EntryNumber.Should().Be(1);

        // JournalValidator ya corrió dentro del pipeline (Post() exige balance) — se reconfirma
        // explícitamente aquí que Σ Debit == Σ Credit para el asiento generado.
        captured.Lines.Sum(l => l.Debit).Should().Be(captured.Lines.Sum(l => l.Credit));
        captured.Lines.Sum(l => l.Debit).Should().Be(23m);
    }

    [Fact]
    public async Task Sin_PostingRule_para_SalesReturn_no_genera_asiento_y_registra_warning()
    {
        var evt = Event();
        var m = new Mocks();

        m.JournalEntries.Setup(r =>
                r.FindByKeyAsync(
                    TenantId,
                    CompanyId,
                    "Sales",
                    "SalesReturn",
                    evt.SalesReturnId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((JournalEntry?)null);
        m.PostingRules.Setup(r =>
                r.FindByKeyAsync(
                    TenantId,
                    CompanyId,
                    "Sales",
                    "SalesReturn",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((PostingRule?)null);

        var engine = m.BuildEngine();
        var translator = m.BuildTranslator(engine);

        var act = async () => await translator.Handle(evt, CancellationToken.None);

        await act.Should().NotThrowAsync();
        m.JournalEntries.Verify(
            r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        m.Logger.Verify(
            l =>
                l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Republicar_el_mismo_evento_es_idempotente()
    {
        var evt = Event();
        var m = new Mocks();
        var existing = JournalEntry.Create(
            TenantId,
            CompanyId,
            DateOnly.FromDateTime(evt.OccurredOn),
            Guid.NewGuid(),
            evt.OccurredOn.Year,
            "Sales",
            "SalesReturn",
            evt.SalesReturnId,
            "Sales — SalesReturn — existing",
            Guid.Empty
        );

        m.JournalEntries.Setup(r =>
                r.FindByKeyAsync(
                    TenantId,
                    CompanyId,
                    "Sales",
                    "SalesReturn",
                    evt.SalesReturnId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(existing);

        var engine = m.BuildEngine();
        var translator = m.BuildTranslator(engine);

        await translator.Handle(evt, CancellationToken.None);

        m.JournalEntries.Verify(
            r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        m.PostingRules.Verify(
            r =>
                r.FindByKeyAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }
}
