using ERP.Domain.Modules.Accounting.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Accounting;

public sealed class JournalEntrySequenceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();

    [Fact]
    public void Create_inicia_en_cero_sin_numero_asignado_todavia()
    {
        var sequence = JournalEntrySequence.Create(TenantId, CompanyId, 2026);

        sequence.CompanyId.Should().Be(CompanyId);
        sequence.FiscalYear.Should().Be(2026);
        sequence.LastNumber.Should().Be(0);
    }

    [Fact]
    public void NextNumber_primera_llamada_devuelve_1()
    {
        var sequence = JournalEntrySequence.Create(TenantId, CompanyId, 2026);

        var number = sequence.NextNumber();

        number.Should().Be(1);
        sequence.LastNumber.Should().Be(1);
    }

    [Fact]
    public void NextNumber_es_correlativo_en_llamadas_sucesivas()
    {
        var sequence = JournalEntrySequence.Create(TenantId, CompanyId, 2026);

        var first = sequence.NextNumber();
        var second = sequence.NextNumber();
        var third = sequence.NextNumber();

        first.Should().Be(1);
        second.Should().Be(2);
        third.Should().Be(3);
        sequence.LastNumber.Should().Be(3);
    }
}
