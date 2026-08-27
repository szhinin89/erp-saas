using ERP.Application.Modules.Accounting.Posting;
using ERP.Domain.Modules.Accounting.Enums;
using FluentAssertions;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// EXPENSES-POSTING-ALLOCATIONS-06: validación fail-fast del value object en su propio
/// constructor — una allocation inválida nunca llega a existir, así que JournalFactory jamás
/// recibe una allocation sin cuenta o con monto no positivo (ver remarks de PostingAllocation.cs).
/// </summary>
public sealed class PostingAllocationTests
{
    [Fact]
    public void Constructor_con_datos_validos_asigna_propiedades()
    {
        var accountId = Guid.NewGuid();
        var sourceLineId = Guid.NewGuid();

        var allocation = new PostingAllocation(
            accountId,
            125.50m,
            AccountNature.Debit,
            "Gasto de suministros",
            sourceLineId
        );

        allocation.AccountingAccountId.Should().Be(accountId);
        allocation.Amount.Should().Be(125.50m);
        allocation.Nature.Should().Be(AccountNature.Debit);
        allocation.Description.Should().Be("Gasto de suministros");
        allocation.SourceLineId.Should().Be(sourceLineId);
    }

    [Fact]
    public void Constructor_sin_cuenta_lanza_ArgumentException()
    {
        var act = () => new PostingAllocation(Guid.Empty, 100m, AccountNature.Debit);

        act.Should()
            .Throw<ArgumentException>()
            .WithParameterName("accountingAccountId");
    }

    [Fact]
    public void Constructor_con_monto_cero_lanza_ArgumentException()
    {
        var act = () => new PostingAllocation(Guid.NewGuid(), 0m, AccountNature.Debit);

        act.Should().Throw<ArgumentException>().WithParameterName("amount");
    }

    [Fact]
    public void Constructor_con_monto_negativo_lanza_ArgumentException()
    {
        var act = () => new PostingAllocation(Guid.NewGuid(), -10m, AccountNature.Debit);

        act.Should().Throw<ArgumentException>().WithParameterName("amount");
    }

    [Fact]
    public void Constructor_con_descripcion_en_blanco_la_normaliza_a_null()
    {
        var allocation = new PostingAllocation(Guid.NewGuid(), 10m, AccountNature.Credit, "   ");

        allocation.Description.Should().BeNull();
    }
}
