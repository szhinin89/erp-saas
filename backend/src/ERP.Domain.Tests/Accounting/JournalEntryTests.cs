using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
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

    private static JournalEntry CreateEntry() =>
        JournalEntry.Create(
            TenantId,
            CompanyId,
            new DateOnly(2026, 7, 25),
            AccountingPeriodId,
            2026,
            "Sales",
            "InvoiceIssued",
            Guid.NewGuid(),
            "Asiento test",
            CreatedBy
        );

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

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*Débito y Crédito simultáneamente*");
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

    // Fase 5.2 — Post(): publicación definitiva del asiento.
    [Fact]
    public void Post_con_asiento_balanceado_cambia_Status_a_Posted_y_registra_PostedAtUtc()
    {
        var entry = CreateEntry();
        entry.AddLine(DebitAccountId, null, 100m, 0m);
        entry.AddLine(CreditAccountId, null, 0m, 100m);
        var before = DateTime.UtcNow;

        entry.Post(CreatedBy, 1);

        entry.Status.Should().Be(JournalEntryStatus.Posted);
        entry.PostedAtUtc.Should().NotBeNull();
        entry.PostedAtUtc!.Value.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Post_con_asiento_desbalanceado_lanza_y_no_cambia_Status()
    {
        var entry = CreateEntry();
        entry.AddLine(DebitAccountId, null, 100m, 0m);
        entry.AddLine(CreditAccountId, null, 0m, 40m);

        var act = () => entry.Post(CreatedBy, 1);

        act.Should().Throw<InvalidOperationException>().WithMessage("*no está balanceado*");
        entry.Status.Should().Be(JournalEntryStatus.Draft);
        entry.PostedAtUtc.Should().BeNull();
        entry.EntryNumber.Should().BeNull();
    }

    [Fact]
    public void Post_no_permite_publicar_dos_veces()
    {
        var entry = CreateEntry();
        entry.AddLine(DebitAccountId, null, 100m, 0m);
        entry.AddLine(CreditAccountId, null, 0m, 100m);
        entry.Post(CreatedBy, 1);

        var act = () => entry.Post(CreatedBy, 2);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Draft*");
        entry.Status.Should().Be(JournalEntryStatus.Posted);
        entry
            .EntryNumber.Should()
            .Be(
                1,
                because: "el número asignado en la primera publicación debe permanecer inmutable"
            );
    }

    [Fact]
    public void Post_sin_lineas_no_lanza_por_balance_pero_publica_en_cero()
    {
        // Documenta el comportamiento actual: EnsureBalanced() con Lines vacío se cumple
        // trivialmente (0 == 0) — la restricción de mínimo dos líneas la impone JournalValidator
        // en el pipeline, no el aggregate. Post() en sí no exige líneas.
        var entry = CreateEntry();

        entry.Post(CreatedBy, 1);

        entry.Status.Should().Be(JournalEntryStatus.Posted);
    }

    // Fase 5.3 — numeración definitiva: EntryNumber se asigna únicamente vía Post().
    [Fact]
    public void Post_asigna_EntryNumber_recibido()
    {
        var entry = CreateEntry();
        entry.AddLine(DebitAccountId, null, 100m, 0m);
        entry.AddLine(CreditAccountId, null, 0m, 100m);

        entry.Post(CreatedBy, 42);

        entry.EntryNumber.Should().Be(42);
    }

    [Fact]
    public void EntryNumber_es_nulo_antes_de_publicar()
    {
        var entry = CreateEntry();

        entry.EntryNumber.Should().BeNull();
    }

    [Fact]
    public void Post_rechaza_entryNumber_menor_a_uno()
    {
        var entry = CreateEntry();
        entry.AddLine(DebitAccountId, null, 100m, 0m);
        entry.AddLine(CreditAccountId, null, 0m, 100m);

        var act = () => entry.Post(CreatedBy, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
        entry.Status.Should().Be(JournalEntryStatus.Draft);
    }

    // Fase 5.4 — Reverse(): reverso contable.
    private static JournalEntry PostedEntry(int entryNumber = 1)
    {
        var entry = CreateEntry();
        entry.AddLine(DebitAccountId, "Débito original", 100m, 0m);
        entry.AddLine(CreditAccountId, "Crédito original", 0m, 100m);
        entry.Post(CreatedBy, entryNumber);
        return entry;
    }

    [Fact]
    public void Reverse_marca_el_original_como_Reversed_y_registra_trazabilidad()
    {
        var original = PostedEntry();
        var before = DateTime.UtcNow;

        var reversal = original.Reverse(CreatedBy, 2, "Error de digitación");

        original.Status.Should().Be(JournalEntryStatus.Reversed);
        original.ReversedAtUtc.Should().NotBeNull();
        original.ReversedAtUtc!.Value.Should().BeOnOrAfter(before);
        original.ReverseReason.Should().Be("Error de digitación");
        original.ReverseJournalEntryId.Should().Be(reversal.Id);
    }

    [Fact]
    public void Reverse_nunca_modifica_los_importes_del_asiento_original()
    {
        var original = PostedEntry();
        var originalDebit = original.Lines.Sum(l => l.Debit);
        var originalCredit = original.Lines.Sum(l => l.Credit);
        var originalLineCount = original.Lines.Count;

        original.Reverse(CreatedBy, 2, "Ajuste");

        original.Lines.Should().HaveCount(originalLineCount);
        original.Lines.Sum(l => l.Debit).Should().Be(originalDebit);
        original.Lines.Sum(l => l.Credit).Should().Be(originalCredit);
    }

    [Fact]
    public void Reverse_crea_un_nuevo_asiento_con_lineas_invertidas_y_balanceado()
    {
        var original = PostedEntry();

        var reversal = original.Reverse(CreatedBy, 2, "Ajuste");

        reversal.Id.Should().NotBe(original.Id);
        reversal.CompanyId.Should().Be(original.CompanyId);
        reversal.AccountingPeriodId.Should().Be(original.AccountingPeriodId);
        reversal.EntryDate.Should().Be(original.EntryDate);
        reversal.Lines.Should().HaveCount(original.Lines.Count);

        reversal
            .Lines.Should()
            .Contain(l => l.AccountId == DebitAccountId && l.Credit == 100m && l.Debit == 0m);
        reversal
            .Lines.Should()
            .Contain(l => l.AccountId == CreditAccountId && l.Debit == 100m && l.Credit == 0m);

        var act = () => reversal.EnsureBalanced();
        act.Should()
            .NotThrow(because: "invertir un asiento ya balanceado preserva Σ Debit == Σ Credit");
    }

    [Fact]
    public void Reverse_publica_el_nuevo_asiento_con_el_EntryNumber_recibido()
    {
        var original = PostedEntry(entryNumber: 1);

        var reversal = original.Reverse(CreatedBy, 7, "Ajuste");

        reversal.Status.Should().Be(JournalEntryStatus.Posted);
        reversal.EntryNumber.Should().Be(7);
        reversal.PostedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Reverse_establece_trazabilidad_bidireccional()
    {
        var original = PostedEntry();

        var reversal = original.Reverse(CreatedBy, 2, "Ajuste");

        reversal.OriginalJournalEntryId.Should().Be(original.Id);
        original.ReverseJournalEntryId.Should().Be(reversal.Id);
    }

    [Fact]
    public void Reverse_rechaza_un_asiento_Draft()
    {
        var entry = CreateEntry();
        entry.AddLine(DebitAccountId, null, 100m, 0m);
        entry.AddLine(CreditAccountId, null, 0m, 100m);

        var act = () => entry.Reverse(CreatedBy, 1, "Ajuste");

        act.Should().Throw<InvalidOperationException>().WithMessage("*Posted*");
    }

    [Fact]
    public void Reverse_rechaza_reversar_dos_veces()
    {
        var original = PostedEntry();
        original.Reverse(CreatedBy, 2, "Primer reverso");

        var act = () => original.Reverse(CreatedBy, 3, "Segundo intento");

        act.Should().Throw<InvalidOperationException>().WithMessage("*Posted*");
        original
            .ReverseReason.Should()
            .Be(
                "Primer reverso",
                because: "el segundo intento de reverso no debe pisar la trazabilidad del primero"
            );
    }

    [Fact]
    public void Reverse_rechaza_motivo_vacio()
    {
        var original = PostedEntry();

        var act = () => original.Reverse(CreatedBy, 2, "   ");

        act.Should().Throw<ArgumentException>();
        original
            .Status.Should()
            .Be(
                JournalEntryStatus.Posted,
                because: "un reverso rechazado no debe dejar el original a medio mutar"
            );
    }
}
