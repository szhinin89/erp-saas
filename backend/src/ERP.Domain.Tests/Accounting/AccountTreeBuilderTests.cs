using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Services;
using ERP.Domain.Modules.Accounting.ValueObjects;
using FluentAssertions;

namespace ERP.Domain.Tests.Accounting;

/// <summary>ACCOUNTING-CHART-CANONICAL-HIERARCHY-01 Fase 7: helper de árbol contable reutilizable.</summary>
public sealed class AccountTreeBuilderTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();

    private static Account NewAccount(string code, Guid? parentId, bool allowsPosting) =>
        Account.Create(
            TenantId,
            CompanyId,
            AccountCode.Create(code),
            $"Cuenta {code}",
            parentId,
            AccountType.Asset,
            AccountNature.Debit,
            allowsPosting,
            ActorId
        );

    [Fact]
    public void Arma_arbol_con_niveles_y_orden_natural_de_codigo()
    {
        var root = NewAccount("1", null, false);
        var mid = NewAccount("1.1", root.Id, false);
        var leaf1 = NewAccount("1.1.10", mid.Id, true);
        var leaf2 = NewAccount("1.1.2", mid.Id, true);

        var tree = AccountTreeBuilder.Build([root, mid, leaf1, leaf2]);

        tree.Should().ContainSingle();
        var rootNode = tree.Single();
        rootNode.Level.Should().Be(0);
        rootNode.Children.Should().ContainSingle();
        var midNode = rootNode.Children.Single();
        midNode.Level.Should().Be(1);
        midNode.Children.Should().HaveCount(2);
        midNode.Children.Select(c => c.Code).Should().Equal("1.1.2", "1.1.10");
    }

    [Fact]
    public void Acumula_saldos_de_hijas_hacia_la_agrupadora()
    {
        var root = NewAccount("1", null, false);
        var leaf1 = NewAccount("1.1", root.Id, true);
        var leaf2 = NewAccount("1.2", root.Id, true);

        var balances = new Dictionary<Guid, decimal> { [leaf1.Id] = 100m, [leaf2.Id] = 50m };

        var tree = AccountTreeBuilder.Build([root, leaf1, leaf2], balances);

        tree.Single().Balance.Should().Be(150m);
    }
}
