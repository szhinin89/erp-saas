using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using FluentAssertions;

namespace ERP.Domain.Tests.Accounting;

/// <summary>
/// Fase 5.5 — AccountingPeriod.Close(readiness): el aggregate rechaza el cierre según el resumen
/// cross-aggregate ya resuelto por IJournalEntryRepository.GetClosureReadinessAsync (Application),
/// sin conocer JournalEntry directamente.
/// </summary>
public sealed class AccountingPeriodTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();

    private static readonly JournalEntryClosureReadiness Ready = new(
        HasDraftOrNonFinalEntries: false,
        HasEntriesWithoutEntryNumber: false,
        HasIncompleteReversals: false
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

    [Fact]
    public void Close_con_readiness_lista_cierra_el_periodo_correctamente()
    {
        var period = OpenPeriod();
        var before = DateTime.UtcNow;

        period.Close(CreatedBy, Ready);

        period.Status.Should().Be(PeriodStatus.Closed);
        period.ClosedAtUtc.Should().NotBeNull();
        period.ClosedAtUtc!.Value.Should().BeOnOrAfter(before);
        period.ClosedBy.Should().Be(CreatedBy);
    }

    [Fact]
    public void Close_falla_si_existen_asientos_Draft_o_no_finales()
    {
        var period = OpenPeriod();
        var readiness = Ready with { HasDraftOrNonFinalEntries = true };

        var act = () => period.Close(CreatedBy, readiness);

        act.Should().Throw<InvalidOperationException>().WithMessage("*sin publicar*");
        period.Status.Should().Be(PeriodStatus.Open);
    }

    [Fact]
    public void Close_falla_si_existen_asientos_sin_EntryNumber()
    {
        var period = OpenPeriod();
        var readiness = Ready with { HasEntriesWithoutEntryNumber = true };

        var act = () => period.Close(CreatedBy, readiness);

        act.Should().Throw<InvalidOperationException>().WithMessage("*número de asiento*");
        period.Status.Should().Be(PeriodStatus.Open);
    }

    [Fact]
    public void Close_falla_si_existen_reversos_incompletos()
    {
        var period = OpenPeriod();
        var readiness = Ready with { HasIncompleteReversals = true };

        var act = () => period.Close(CreatedBy, readiness);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*reversos contables incompletos*");
        period.Status.Should().Be(PeriodStatus.Open);
    }

    [Fact]
    public void Close_falla_con_multiples_motivos_simultaneos_y_los_reporta_todos()
    {
        var period = OpenPeriod();
        var readiness = new JournalEntryClosureReadiness(
            HasDraftOrNonFinalEntries: true,
            HasEntriesWithoutEntryNumber: true,
            HasIncompleteReversals: true
        );

        var act = () => period.Close(CreatedBy, readiness);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*sin publicar*")
            .WithMessage("*número de asiento*")
            .Which.Message.Should()
            .Contain("reversos contables incompletos");
    }

    [Fact]
    public void Close_sobre_periodo_ya_cerrado_lanza_sin_reevaluar_readiness()
    {
        var period = OpenPeriod();
        period.Close(CreatedBy, Ready);

        var act = () => period.Close(CreatedBy, Ready);

        act.Should().Throw<InvalidOperationException>().WithMessage("*no admite contabilización*");
    }

    [Fact]
    public void JournalEntryClosureReadiness_IsReady_es_false_si_cualquier_bandera_esta_activa()
    {
        Ready.IsReady.Should().BeTrue();
        (Ready with { HasDraftOrNonFinalEntries = true }).IsReady.Should().BeFalse();
        (Ready with { HasEntriesWithoutEntryNumber = true }).IsReady.Should().BeFalse();
        (Ready with { HasIncompleteReversals = true }).IsReady.Should().BeFalse();
    }
}
