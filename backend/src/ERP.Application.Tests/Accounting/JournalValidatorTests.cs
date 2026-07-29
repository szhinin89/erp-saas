using ERP.Application.Modules.Accounting.Posting;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// JournalValidator es <c>internal</c> — se ejercita indirectamente vía
/// <see cref="PostingEngine.PostAsync"/> con repositorios mockeados, mismo criterio que
/// <see cref="PostingEngineTests"/>/<see cref="JournalFactoryTests"/>. Cubre las validaciones de
/// Fase 3.5.5: balance, mínimo de líneas, duplicados inválidos, montos en cero.
/// </summary>
public sealed class JournalValidatorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();

    private static PostingFact Fact(
        decimal subtotal = 100m,
        decimal totalVat = 15m,
        decimal totalIce = 0m,
        decimal totalDiscount = 0m,
        decimal grandTotal = 115m
    ) =>
        new(
            TenantId,
            CompanyId,
            "Sales",
            "InvoiceIssued",
            Guid.NewGuid(),
            new DateOnly(2026, 7, 15),
            subtotal,
            totalVat,
            totalIce,
            totalDiscount,
            grandTotal
        );

    private static AccountingPeriod OpenPeriod() =>
        AccountingPeriod.Create(
            TenantId,
            CompanyId,
            2026,
            7,
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 31),
            CreatedBy
        );

    private static PostingRule EmptyRule() =>
        PostingRule.Create(
            TenantId,
            CompanyId,
            "Sales",
            "InvoiceIssued",
            null,
            null,
            null,
            CreatedBy
        );

    private sealed class Mocks
    {
        public Mock<IJournalEntryRepository> JournalEntries { get; } = new();
        public Mock<IPostingRuleRepository> PostingRules { get; } = new();
        public Mock<IAccountingPeriodRepository> AccountingPeriods { get; } = new();
        public Mock<IJournalEntrySequenceRepository> JournalEntrySequences { get; } = new();

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
            JournalEntries
                .Setup(r =>
                    r.FindByKeyAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<Guid>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<Guid>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync((JournalEntry?)null);
            AccountingPeriods
                .Setup(r =>
                    r.FindContainingDateAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<Guid>(),
                        It.IsAny<DateOnly>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(OpenPeriod());
            JournalEntrySequences
                .Setup(r =>
                    r.ReserveNextNumberAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<Guid>(),
                        It.IsAny<int>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(1);
        }

        public void SetupRule(PostingRule rule) =>
            PostingRules
                .Setup(r =>
                    r.FindByKeyAsync(
                        TenantId,
                        CompanyId,
                        "Sales",
                        "InvoiceIssued",
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(rule);

        public PostingEngine BuildEngine() =>
            new(
                JournalEntries.Object,
                PostingRules.Object,
                AccountingPeriods.Object,
                JournalEntrySequences.Object
            );
    }

    [Fact]
    public async Task Regla_balanceada_produce_posting_exitoso()
    {
        var rule = EmptyRule();
        rule.AddLine(Guid.NewGuid(), AccountNature.Debit, PostingAmountKind.GrandTotal);
        rule.AddLine(Guid.NewGuid(), AccountNature.Credit, PostingAmountKind.Subtotal);
        rule.AddLine(Guid.NewGuid(), AccountNature.Credit, PostingAmountKind.TaxVat);

        var m = new Mocks();
        m.SetupRule(rule);

        var result = await m.BuildEngine().PostAsync(Fact());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(PostingOutcomeStatus.Created);
    }

    [Fact]
    public async Task Regla_desbalanceada_falla_con_VALIDATION_FAILED()
    {
        // Debit GrandTotal (115) vs Credit Subtotal (100) — no balancea porque falta el
        // componente TaxVat en la regla (error de configuración deliberado para el test).
        var rule = EmptyRule();
        rule.AddLine(Guid.NewGuid(), AccountNature.Debit, PostingAmountKind.GrandTotal);
        rule.AddLine(Guid.NewGuid(), AccountNature.Credit, PostingAmountKind.Subtotal);

        var m = new Mocks();
        m.SetupRule(rule);

        var result = await m.BuildEngine().PostAsync(Fact());

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be("VALIDATION_FAILED");
        result.Error.Should().Contain("no está balanceado");
        m.JournalEntries.Verify(
            r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Regla_con_una_sola_linea_falla_por_minimo_de_lineas()
    {
        var rule = EmptyRule();
        rule.AddLine(Guid.NewGuid(), AccountNature.Debit, PostingAmountKind.GrandTotal);

        var m = new Mocks();
        m.SetupRule(rule);

        var result = await m.BuildEngine().PostAsync(Fact());

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be("VALIDATION_FAILED");
        result.Error.Should().Contain("al menos dos líneas");
    }

    [Fact]
    public async Task Regla_sin_ninguna_linea_configurada_falla_por_cuentas_faltantes()
    {
        // "Cuentas faltantes": una regla sin PostingRuleLine no tiene ninguna cuenta mapeada —
        // JournalFactory genera un asiento sin líneas, que el Validator rechaza.
        var rule = EmptyRule();

        var m = new Mocks();
        m.SetupRule(rule);

        var result = await m.BuildEngine().PostAsync(Fact());

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public async Task Regla_con_misma_cuenta_en_debito_y_credito_falla()
    {
        var sameAccount = Guid.NewGuid();
        var rule = EmptyRule();
        rule.AddLine(sameAccount, AccountNature.Debit, PostingAmountKind.Subtotal);
        rule.AddLine(sameAccount, AccountNature.Credit, PostingAmountKind.Subtotal);

        var m = new Mocks();
        m.SetupRule(rule);

        var result = await m.BuildEngine().PostAsync(Fact(totalVat: 0m, grandTotal: 100m));

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be("VALIDATION_FAILED");
        result.Error.Should().Contain("no puede recibir Débito y Crédito");
    }
}
