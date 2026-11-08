using ERP.Application.Modules.Purchases.PurchaseReception.Services;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using FluentAssertions;

namespace ERP.Application.Tests.Purchases.PurchaseReception;

/// <summary>
/// PURCHASES-RECEPTION-RECEIVER-RUC-01 (H1) — regla única de pertenencia del receptor. Identificaciones
/// sintéticas (estructuralmente válidas, no de personas reales).
/// </summary>
public sealed class PurchaseReceptionReceiverRuleTests
{
    private const string SocietyRuc = "1790000000001";
    private const string NaturalCedula = "1700000001";
    private const string NaturalRuc = "1700000001001";

    [Theory]
    [InlineData(SocietyRuc, SocietyRuc, PurchaseReceptionReceiverMatch.Match)]
    [InlineData(SocietyRuc, " 1790000000001 ", PurchaseReceptionReceiverMatch.Match)]
    [InlineData(NaturalRuc, NaturalRuc, PurchaseReceptionReceiverMatch.Match)]
    [InlineData(NaturalRuc, NaturalCedula, PurchaseReceptionReceiverMatch.Match)]
    [InlineData(SocietyRuc, "0990000000001", PurchaseReceptionReceiverMatch.Different)]
    [InlineData(NaturalRuc, "0900000001", PurchaseReceptionReceiverMatch.Different)]
    [InlineData(SocietyRuc, NaturalCedula, PurchaseReceptionReceiverMatch.Different)]
    [InlineData(SocietyRuc, null, PurchaseReceptionReceiverMatch.Missing)]
    [InlineData(SocietyRuc, "   ", PurchaseReceptionReceiverMatch.Missing)]
    [InlineData(NaturalRuc, "1700000002", PurchaseReceptionReceiverMatch.Invalid)]
    [InlineData(SocietyRuc, "1790000000000", PurchaseReceptionReceiverMatch.Invalid)]
    [InlineData(SocietyRuc, "17900000A0001", PurchaseReceptionReceiverMatch.Invalid)]
    [InlineData(SocietyRuc, "17000000010", PurchaseReceptionReceiverMatch.Invalid)]
    public void Evaluate_classifies_the_receiver(
        string companyRuc,
        string? receiver,
        PurchaseReceptionReceiverMatch expected
    ) => PurchaseReceptionReceiverRule.Evaluate(companyRuc, receiver).Should().Be(expected);

    [Fact]
    public void Cedula_equivalence_requires_exactly_the_cedula_plus_001()
    {
        PurchaseReceptionReceiverRule.Evaluate("1700000001002", NaturalCedula)
            .Should().Be(PurchaseReceptionReceiverMatch.Different);
    }

    [Fact]
    public void Cedula_equivalence_does_not_apply_to_public_entities()
    {
        // Tercer dígito 6: cédula estructuralmente válida, pero el RUC público no es cédula + 001.
        PurchaseReceptionReceiverRule.Evaluate("1760000008001", "1760000008")
            .Should().Be(PurchaseReceptionReceiverMatch.Different);
    }

    [Theory]
    [InlineData(SocietyRuc, false, true)]
    [InlineData(SocietyRuc, true, false)]
    [InlineData("1790000000000", false, false)]
    [InlineData(null, false, false)]
    public void Company_ruc_must_be_definitive_and_valid(string? ruc, bool temporary, bool expected) =>
        PurchaseReceptionReceiverRule.IsCompanyRucUsable(ruc, temporary).Should().Be(expected);

    [Fact]
    public void FindRejection_returns_null_when_every_row_belongs_to_the_company()
    {
        PurchaseReceptionReceiverRule
            .FindRejection(NaturalRuc, [Row(2, NaturalCedula), Row(3, NaturalRuc)])
            .Should().BeNull();
    }

    [Fact]
    public void FindRejection_lists_lines_by_category_without_the_foreign_identification()
    {
        var message = PurchaseReceptionReceiverRule.FindRejection(
            SocietyRuc,
            [Row(2, SocietyRuc), Row(3, "0990000000001"), Row(4, null), Row(5, "17900000A0001")]
        );

        message.Should().StartWith("No se importó ningún documento");
        message.Should().Contain("no corresponden a la empresa activa (líneas 3)");
        message.Should().Contain("no indican la identificación del receptor (líneas 4)");
        message.Should().Contain("identificación del receptor inválida (líneas 5)");
        message.Should().NotContain("0990000000001").And.NotContain("17900000A0001");
    }

    [Fact]
    public void FindRejection_truncates_long_line_lists()
    {
        var rows = Enumerable.Range(2, 15).Select(n => Row(n, "0990000000001")).ToList();

        PurchaseReceptionReceiverRule.FindRejection(SocietyRuc, rows)
            .Should().Contain("(líneas 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 y 5 más)");
    }

    private static PurchaseReceptionRecord Row(int line, string? receiver) =>
        new(
            line,
            PurchaseReceptionSourceDocType.Invoice,
            "1791111111001",
            "Proveedor",
            "001-001-000000001",
            $"key-{line}",
            new DateOnly(2026, 7, 1),
            new DateTime(2026, 7, 1, 10, 0, 0),
            receiver,
            1m,
            0.15m,
            1.15m,
            null
        );
}
