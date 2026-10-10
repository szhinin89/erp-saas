using ERP.Domain.Exceptions;
using ERP.Domain.Modules.InitialLoad.Constants;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.InitialLoad;

/// <summary>IL-7A — estado contable de la apertura por lote (Pending / Posted / Failed).</summary>
public sealed class OpeningBalancePostingTests
{
    private static readonly DateOnly Cutoff = new(2026, 9, 30);

    private static OpeningBalancePosting Pending(
        ImportType type = ImportType.InitialStock,
        decimal amount = 100m
    ) =>
        OpeningBalancePosting.CreatePending(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            type,
            Cutoff,
            amount,
            Guid.NewGuid()
        );

    [Theory]
    [InlineData(ImportType.InitialStock, OpeningBalancePostingFacts.OpeningInventory)]
    [InlineData(ImportType.InitialReceivables, OpeningBalancePostingFacts.OpeningReceivables)]
    [InlineData(ImportType.InitialPayables, OpeningBalancePostingFacts.OpeningPayables)]
    public void Lote_de_saldos_nace_pendiente_con_su_hecho_contable(ImportType type, string factType)
    {
        var posting = Pending(type);

        posting.Status.Should().Be(OpeningBalancePostingStatus.Pending);
        posting.FactType.Should().Be(factType);
        posting.EntryDate.Should().Be(Cutoff);
        posting.JournalEntryId.Should().BeNull();
        posting.Attempts.Should().Be(0);
    }

    [Theory]
    [InlineData(ImportType.Customers)]
    [InlineData(ImportType.Suppliers)]
    [InlineData(ImportType.Items)]
    [InlineData(ImportType.Prices)]
    public void Maestros_y_catalogos_no_generan_apertura(ImportType type)
    {
        OpeningBalancePostingFacts.ForImportType(type).Should().BeNull();
        var act = () => Pending(type);
        act.Should().Throw<DomainRuleViolationException>();
    }

    [Theory]
    [InlineData(10.005, 10.01)]
    [InlineData(10.004, 10.00)]
    [InlineData(1234.5650, 1234.57)]
    public void Monto_se_redondea_a_dos_decimales_away_from_zero(decimal raw, decimal expected)
    {
        Pending(amount: raw).Amount.Should().Be(expected);
        OpeningBalancePosting.RoundAmount(raw).Should().Be(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.004)]
    [InlineData(-5)]
    public void Monto_no_positivo_tras_redondeo_se_rechaza(decimal raw)
    {
        var act = () => Pending(amount: raw);
        act.Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void Posted_es_terminal_e_idempotente_con_el_mismo_asiento()
    {
        var posting = Pending();
        var entryId = Guid.NewGuid();

        posting.MarkPosted(entryId, Guid.NewGuid());
        posting.MarkPosted(entryId, Guid.NewGuid());

        posting.Status.Should().Be(OpeningBalancePostingStatus.Posted);
        posting.JournalEntryId.Should().Be(entryId);
        posting.PostedAt.Should().NotBeNull();
        posting.Attempts.Should().Be(1);

        var other = () => posting.MarkPosted(Guid.NewGuid(), Guid.NewGuid());
        other.Should().Throw<DomainRuleViolationException>();
        var fail = () => posting.MarkFailed("X", "y", Guid.NewGuid());
        fail.Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void Failed_es_reintentable_y_un_posteo_posterior_limpia_el_error()
    {
        var posting = Pending();

        posting.MarkFailed("PERIOD_NOT_OPEN", new string('x', 3000), Guid.NewGuid());

        posting.Status.Should().Be(OpeningBalancePostingStatus.Failed);
        posting.ErrorCode.Should().Be("PERIOD_NOT_OPEN");
        posting.ErrorMessage.Should().HaveLength(OpeningBalancePosting.ErrorMessageMaxLength);

        posting.MarkFailed("RULE_NOT_FOUND", "sin regla", Guid.NewGuid());
        posting.MarkPosted(Guid.NewGuid(), Guid.NewGuid());

        posting.Status.Should().Be(OpeningBalancePostingStatus.Posted);
        posting.ErrorCode.Should().BeNull();
        posting.ErrorMessage.Should().BeNull();
        posting.Attempts.Should().Be(3);
    }
}
