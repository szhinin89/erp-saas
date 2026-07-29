using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Accounting;

public sealed class PostingRuleLineTests
{
    private static readonly Guid PostingRuleId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();

    [Fact]
    public void Create_con_datos_validos_asigna_todos_los_campos()
    {
        var line = PostingRuleLine.Create(
            PostingRuleId,
            TenantId,
            AccountId,
            AccountNature.Debit,
            PostingAmountKind.Subtotal,
            sortOrder: 0
        );

        line.PostingRuleId.Should().Be(PostingRuleId);
        line.TenantId.Should().Be(TenantId);
        line.AccountId.Should().Be(AccountId);
        line.Nature.Should().Be(AccountNature.Debit);
        line.AmountKind.Should().Be(PostingAmountKind.Subtotal);
        line.SortOrder.Should().Be(0);
    }

    [Theory]
    [InlineData(AccountNature.Debit)]
    [InlineData(AccountNature.Credit)]
    public void Create_persiste_la_naturaleza_indicada(AccountNature nature)
    {
        var line = PostingRuleLine.Create(
            PostingRuleId,
            TenantId,
            AccountId,
            nature,
            PostingAmountKind.TaxVat,
            0
        );

        line.Nature.Should().Be(nature);
    }

    [Theory]
    [InlineData(PostingAmountKind.Subtotal)]
    [InlineData(PostingAmountKind.TaxVat)]
    [InlineData(PostingAmountKind.TaxIce)]
    [InlineData(PostingAmountKind.Discount)]
    [InlineData(PostingAmountKind.Retention)]
    [InlineData(PostingAmountKind.GrandTotal)]
    public void Create_persiste_el_AmountKind_indicado(PostingAmountKind kind)
    {
        var line = PostingRuleLine.Create(
            PostingRuleId,
            TenantId,
            AccountId,
            AccountNature.Debit,
            kind,
            0
        );

        line.AmountKind.Should().Be(kind);
    }

    [Fact]
    public void Create_con_AccountId_vacio_lanza_excepcion()
    {
        var act = () =>
            PostingRuleLine.Create(
                PostingRuleId,
                TenantId,
                Guid.Empty,
                AccountNature.Debit,
                PostingAmountKind.Subtotal,
                0
            );

        act.Should().Throw<ArgumentException>();
    }
}
