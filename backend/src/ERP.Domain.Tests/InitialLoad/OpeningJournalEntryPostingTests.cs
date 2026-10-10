using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.DocTypes.Constants;
using ERP.Domain.Modules.InitialLoad.Constants;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using FluentAssertions;
using CompanyEntity = ERP.Domain.Modules.Company.Entities.Company;

namespace ERP.Domain.Tests.InitialLoad;

/// <summary>IL-8A — estado del ASI de apertura (Pending / Posted / Failed) y fecha de apertura inmutable.</summary>
public sealed class OpeningJournalEntryPostingTests
{
    private static readonly DateOnly Cutoff = new(2026, 9, 30);

    private static OpeningJournalEntryPosting Pending(decimal total = 100m, int lines = 2) =>
        OpeningJournalEntryPosting.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 1, Cutoff, total, lines, Guid.NewGuid());

    [Fact]
    public void El_hecho_del_ASI_reutiliza_el_codigo_ManualJournalEntry()
    {
        OpeningBalancePostingFacts.OpeningJournalEntry.Should().Be(DocTypeCodes.ManualJournalEntry);
        OpeningBalancePostingFacts.OpeningJournalEntry.Should().Be("ASI");
    }

    [Fact]
    public void Nace_pendiente_con_fecha_total_redondeado_y_lineas()
    {
        var posting = Pending(10.005m, 3);

        posting.Status.Should().Be(OpeningBalancePostingStatus.Pending);
        posting.Version.Should().Be(1);
        posting.IsCurrent.Should().BeTrue();
        posting.SupersededAt.Should().BeNull();
        posting.EntryDate.Should().Be(Cutoff);
        posting.TotalAmount.Should().Be(10.01m);
        posting.LineCount.Should().Be(3);
        posting.JournalEntryId.Should().BeNull();
        posting.Attempts.Should().Be(0);
        posting.Id.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(0.004, 2)]
    [InlineData(10, 1)]
    public void Total_no_positivo_o_menos_de_dos_lineas_se_rechaza(decimal total, int lines)
    {
        var act = () => Pending(total, lines);

        act.Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void Failed_es_reintentable_y_Posted_es_terminal_e_idempotente()
    {
        var posting = Pending();
        posting.MarkFailed("OPENING_BRIDGE_NOT_CLEARED", "x", Guid.NewGuid());
        posting.Status.Should().Be(OpeningBalancePostingStatus.Failed);

        posting.PrepareAttempt(Cutoff.AddDays(-1), 50m, 4, Guid.NewGuid());
        var journal = Guid.NewGuid();
        posting.MarkPosted(journal, Guid.NewGuid());
        posting.MarkPosted(journal, Guid.NewGuid());

        posting.Status.Should().Be(OpeningBalancePostingStatus.Posted);
        posting.EntryDate.Should().Be(Cutoff.AddDays(-1));
        posting.TotalAmount.Should().Be(50m);
        posting.ErrorCode.Should().BeNull();
        posting.Attempts.Should().Be(2);
        posting.Invoking(p => p.MarkPosted(Guid.NewGuid(), Guid.NewGuid()))
            .Should().Throw<DomainRuleViolationException>();
        posting.Invoking(p => p.MarkFailed("X", "x", Guid.NewGuid()))
            .Should().Throw<DomainRuleViolationException>();
        posting.Invoking(p => p.PrepareAttempt(Cutoff, 10m, 2, Guid.NewGuid()))
            .Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void Version_menor_a_1_se_rechaza()
    {
        var act = () => OpeningJournalEntryPosting.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 0, Cutoff, 10m, 2, Guid.NewGuid());

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Una_version_no_publicada_no_se_reemplaza_se_reintenta()
    {
        var pending = Pending();
        pending.Invoking(p => p.MarkSuperseded(Guid.NewGuid())).Should().Throw<DomainRuleViolationException>();
        pending.MarkFailed("X", "x", Guid.NewGuid());
        pending.Invoking(p => p.MarkSuperseded(Guid.NewGuid())).Should().Throw<DomainRuleViolationException>();
        pending.IsCurrent.Should().BeTrue();
    }

    [Fact]
    public void Version_publicada_reemplazada_queda_como_historial_inmutable_y_el_reemplazo_tiene_Id_nuevo()
    {
        var company = Guid.NewGuid();
        var v1 = OpeningJournalEntryPosting.CreatePending(Guid.NewGuid(), company, 1, Cutoff, 100m, 2, Guid.NewGuid());
        var journal = Guid.NewGuid();
        v1.MarkPosted(journal, Guid.NewGuid());

        v1.MarkSuperseded(Guid.NewGuid());
        var v2 = OpeningJournalEntryPosting.CreatePending(v1.TenantId, company, v1.Version + 1, Cutoff, 80m, 3, Guid.NewGuid());

        v1.IsCurrent.Should().BeFalse();
        v1.SupersededAt.Should().NotBeNull();
        v1.Status.Should().Be(OpeningBalancePostingStatus.Posted, "el historial conserva su estado y asiento");
        v1.JournalEntryId.Should().Be(journal);
        v1.Invoking(p => p.MarkPosted(journal, Guid.NewGuid())).Should().Throw<DomainRuleViolationException>();
        v1.Invoking(p => p.MarkFailed("X", "x", Guid.NewGuid())).Should().Throw<DomainRuleViolationException>();
        v1.Invoking(p => p.PrepareAttempt(Cutoff, 1m, 2, Guid.NewGuid())).Should().Throw<DomainRuleViolationException>();
        v1.Invoking(p => p.MarkSuperseded(Guid.NewGuid())).Should().Throw<DomainRuleViolationException>();
        v2.Id.Should().NotBe(v1.Id, "SourceEventId nuevo: no choca con la idempotencia del Posting Engine");
        v2.Version.Should().Be(2);
        v2.IsCurrent.Should().BeTrue();
        v2.Status.Should().Be(OpeningBalancePostingStatus.Pending);
        v1.PostedAt.Should().NotBeNull("el historial conserva la marca de publicado que fija la fecha de apertura");
    }

    [Fact]
    public void Con_ASI_publicado_la_fecha_de_apertura_es_definitiva()
    {
        var company = CompanyEntity.CreateManaged(Guid.NewGuid(), "1790012345001", "Apertura", createdBy: Guid.NewGuid());
        company.SetOpeningBalanceDate(Cutoff, new OpeningBalanceDateConstraints(false, []), null);
        var posted = new OpeningBalanceDateConstraints(false, [], HasPostedOpeningJournalEntry: true);

        CompanyEntity.OpeningBalanceDateLockReason(Cutoff, posted).Should().Contain("ASI");
        company.Invoking(c => c.SetOpeningBalanceDate(Cutoff.AddDays(-1), posted, null))
            .Should().Throw<DomainRuleViolationException>().WithMessage("*asiento de apertura*");
        company.Invoking(c => c.SetOpeningBalanceDate(Cutoff, posted, null)).Should().NotThrow();
        CompanyEntity.OpeningBalanceDateLockReason(Cutoff, new OpeningBalanceDateConstraints(false, [])).Should().BeNull();
    }
}
