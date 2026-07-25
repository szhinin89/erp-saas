using ERP.Domain.Modules.Accounting.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Accounting;

public sealed class JournalEntryTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid AccountingPeriodId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();
    private static readonly Guid DebitAccountId = Guid.NewGuid();
    private static readonly Guid CreditAccountId = Guid.NewGuid();

    private static JournalEntry CreateEntry() => JournalEntry.Create(
        TenantId, CompanyId, new DateOnly(2026, 7, 25), AccountingPeriodId,
        "Sales", "InvoiceIssued", Guid.NewGuid(), "Asiento test", CreatedBy);

    [Fact]
    public void Create_sin_lineas_expone_Lines_vacio()
    {
        var entry = CreateEntry();

        entry.Lines.Should().BeEmpty();
    }

    [Fact]
    public void AddLine_agrega_lineas_al_asiento()
    {
        var entry = CreateEntry();

        entry.AddLine(DebitAccountId, "Débito", 100m, 0m);
        entry.AddLine(CreditAccountId, "Crédito", 0m, 100m);

        entry.Lines.Should().HaveCount(2);
        entry.Lines.Should().Contain(l => l.AccountId == DebitAccountId && l.Debit == 100m);
        entry.Lines.Should().Contain(l => l.AccountId == CreditAccountId && l.Credit == 100m);
    }

    [Fact]
    public void AddLine_asigna_SortOrder_incremental()
    {
        var entry = CreateEntry();

        entry.AddLine(DebitAccountId, null, 100m, 0m);
        entry.AddLine(CreditAccountId, null, 0m, 100m);

        entry.Lines.Select(l => l.SortOrder).Should().Equal((short)0, (short)1);
    }

    [Fact]
    public void Lines_es_de_solo_lectura_para_el_consumidor()
    {
        var entry = CreateEntry();
        entry.AddLine(DebitAccountId, null, 100m, 0m);

        entry.Lines.Should().BeAssignableTo<IReadOnlyCollection<JournalEntryLine>>();

        // Único mecanismo público para modificar las líneas es AddLine — no existe
        // Add()/Remove()/Clear() expuesto sobre la colección devuelta.
        entry.Lines.GetType().GetMethod("Add").Should().BeNull();
    }

    [Fact]
    public void EnsureBalanced_sin_lineas_no_lanza()
    {
        var entry = CreateEntry();

        var act = () => entry.EnsureBalanced();

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureBalanced_con_debitos_igual_a_creditos_no_lanza()
    {
        var entry = CreateEntry();
        entry.AddLine(DebitAccountId, null, 100m, 0m);
        entry.AddLine(CreditAccountId, null, 0m, 100m);

        var act = () => entry.EnsureBalanced();

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureBalanced_con_debitos_distintos_de_creditos_lanza()
    {
        var entry = CreateEntry();
        entry.AddLine(DebitAccountId, null, 100m, 0m);
        entry.AddLine(CreditAccountId, null, 0m, 40m);

        var act = () => entry.EnsureBalanced();

        act.Should().Throw<InvalidOperationException>().WithMessage("*no está balanceado*");
    }

    // Fase 3.5.6 — endurecimiento: AddLine() es el único punto público de entrada para agregar
    // líneas al aggregate; estos tests confirman que la validación de JournalEntryLine.Create()
    // (ya cubierta de forma aislada en JournalEntryLineTests) también se cumple al pasar por el
    // límite público del aggregate, no solo llamando a la entidad hija directamente.
    [Fact]
    public void AddLine_rechaza_Debit_y_Credit_ambos_con_valor()
    {
        var entry = CreateEntry();

        var act = () => entry.AddLine(DebitAccountId, null, 100m, 50m);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Débito y Crédito simultáneamente*");
    }

    [Fact]
    public void AddLine_rechaza_Debit_y_Credit_ambos_en_cero()
    {
        var entry = CreateEntry();

        var act = () => entry.AddLine(DebitAccountId, null, 0m, 0m);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Débito o en Crédito*");
    }

    [Fact]
    public void AddLine_rechaza_AccountId_vacio()
    {
        var entry = CreateEntry();

        var act = () => entry.AddLine(Guid.Empty, null, 100m, 0m);

        act.Should().Throw<ArgumentException>();
    }
}
