using ERP.Domain.Modules.Accounting.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Accounting;

public sealed class JournalEntryLineTests
{
    private static readonly Guid JournalEntryId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();

    private static JournalEntryLine CreateLine(decimal debit, decimal credit) =>
        JournalEntryLine.Create(
            JournalEntryId,
            TenantId,
            AccountId,
            "Línea test",
            debit,
            credit,
            sortOrder: 0
        );

    [Fact]
    public void Create_con_Debit_valido_asigna_Debit_y_dejaCredit_en_cero()
    {
        var line = CreateLine(100m, 0m);

        line.Debit.Should().Be(100m);
        line.Credit.Should().Be(0m);
    }

    [Fact]
    public void Create_con_Credit_valido_asigna_Credit_y_deja_Debit_en_cero()
    {
        var line = CreateLine(0m, 100m);

        line.Credit.Should().Be(100m);
        line.Debit.Should().Be(0m);
    }

    [Fact]
    public void Create_con_Debit_y_Credit_ambos_con_valor_lanza_excepcion()
    {
        var act = () => CreateLine(100m, 50m);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*Débito y Crédito simultáneamente*");
    }

    [Fact]
    public void Create_con_Debit_y_Credit_ambos_en_cero_lanza_excepcion()
    {
        var act = () => CreateLine(0m, 0m);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Débito o en Crédito*");
    }

    [Fact]
    public void Create_con_AccountId_vacio_lanza_excepcion()
    {
        var act = () =>
            JournalEntryLine.Create(JournalEntryId, TenantId, Guid.Empty, null, 100m, 0m, 0);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void Create_con_monto_negativo_lanza_excepcion(decimal debit, decimal credit)
    {
        var act = () => CreateLine(debit, credit);

        act.Should().Throw<ArgumentException>();
    }
}
