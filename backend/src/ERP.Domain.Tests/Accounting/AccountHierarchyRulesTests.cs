using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Services;
using ERP.Domain.Modules.Accounting.ValueObjects;
using FluentAssertions;

namespace ERP.Domain.Tests.Accounting;

/// <summary>
/// ACCOUNTING-CHART-CANONICAL-HIERARCHY-01 Fase 1: el código contable manda la jerarquía.
/// ACCOUNTING-CHART-INTEGRITY-01: hijas (I1), descendientes de cualquier nivel (I3) y cadena de
/// ancestros (I4), con protección contra ciclos.
/// </summary>
public sealed class AccountHierarchyRulesTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static Account NewAccount(string code, Account? parent = null, bool active = true)
    {
        var account = Account.Create(
            TenantId,
            CompanyId,
            AccountCode.Create(code),
            $"Cuenta {code}",
            parent?.Id,
            AccountType.Asset,
            AccountNature.Debit,
            false,
            UserId
        );
        if (!active)
            account.Disable(UserId);
        return account;
    }

    [Theory]
    [InlineData("1", null)]
    [InlineData("1.1", "1")]
    [InlineData("1.1.01", "1.1")]
    [InlineData("1.1.01.001", "1.1.01")]
    public void GetExpectedParentCode_devuelve_el_prefijo_inmediato(string code, string? expected)
    {
        AccountHierarchyRules.GetExpectedParentCode(code).Should().Be(expected);
    }

    [Theory]
    [InlineData("1", 0)]
    [InlineData("1.1", 1)]
    [InlineData("1.1.01", 2)]
    [InlineData("1.1.01.001", 3)]
    public void GetCodeDepth_cuenta_segmentos_menos_uno(string code, int expected)
    {
        AccountHierarchyRules.GetCodeDepth(code).Should().Be(expected);
    }

    [Fact]
    public void HasChildren_cuenta_hijas_activas_e_inactivas()
    {
        var root = NewAccount("1");
        var withInactiveChild = NewAccount("2");
        var leaf = NewAccount("3");
        var accounts = new[] { root, NewAccount("1.1", root), withInactiveChild,
            NewAccount("2.1", withInactiveChild, active: false), leaf };

        AccountHierarchyRules.HasChildren(root.Id, accounts).Should().BeTrue();
        AccountHierarchyRules.HasChildren(withInactiveChild.Id, accounts).Should().BeTrue();
        AccountHierarchyRules.HasChildren(leaf.Id, accounts).Should().BeFalse();
    }

    [Fact]
    public void HasActiveDescendant_detecta_nieta_activa_bajo_hija_inactiva()
    {
        var root = NewAccount("1");
        var child = NewAccount("1.1", root, active: false);
        var grandchild = NewAccount("1.1.01", child);

        AccountHierarchyRules.HasActiveDescendant(root.Id, new[] { root, child, grandchild })
            .Should().BeTrue();
    }

    [Fact]
    public void HasActiveDescendant_false_si_todo_el_subarbol_esta_inactivo_o_es_hoja()
    {
        var root = NewAccount("1");
        var child = NewAccount("1.1", root, active: false);
        var grandchild = NewAccount("1.1.01", child, active: false);
        var accounts = new[] { root, child, grandchild };

        AccountHierarchyRules.HasActiveDescendant(root.Id, accounts).Should().BeFalse();
        AccountHierarchyRules.HasActiveDescendant(grandchild.Id, accounts).Should().BeFalse();
    }

    [Fact]
    public void HasInactiveAncestor_recorre_la_cadena_completa()
    {
        var root = NewAccount("1", active: false);
        var child = NewAccount("1.1", root);
        var grandchild = NewAccount("1.1.01", child);
        var accounts = new[] { root, child, grandchild };

        AccountHierarchyRules.HasInactiveAncestor(grandchild, accounts).Should().BeTrue();
        AccountHierarchyRules.HasInactiveAncestor(root, accounts).Should().BeFalse();
    }

    [Fact]
    public void HasInactiveAncestor_false_con_ancestros_activos_o_padre_ausente()
    {
        var root = NewAccount("1");
        var child = NewAccount("1.1", root);
        var orphan = NewAccount("9.1");
        orphan.UpdateParent(Guid.NewGuid(), UserId);

        AccountHierarchyRules.HasInactiveAncestor(child, new[] { root, child }).Should().BeFalse();
        AccountHierarchyRules.HasInactiveAncestor(orphan, new[] { orphan }).Should().BeFalse();
    }

    [Fact]
    public void HasInactiveAncestor_desde_el_padre_incluye_al_propio_padre_y_su_cadena()
    {
        var root = NewAccount("1", active: false);
        var child = NewAccount("1.1", root);
        var activeRoot = NewAccount("2");
        var activeChild = NewAccount("2.1", activeRoot);
        var accounts = new[] { root, child, activeRoot, activeChild };

        AccountHierarchyRules.HasInactiveAncestor(root.Id, accounts).Should().BeTrue("el padre está inactivo");
        AccountHierarchyRules.HasInactiveAncestor(child.Id, accounts).Should().BeTrue("el abuelo está inactivo");
        AccountHierarchyRules.HasInactiveAncestor(activeChild.Id, accounts).Should().BeFalse();
        AccountHierarchyRules.HasInactiveAncestor((Guid?)null, accounts).Should().BeFalse("cuenta raíz");
    }

    [Fact]
    public void Recorridos_terminan_ante_un_ciclo_existente()
    {
        var a = NewAccount("1");
        var b = NewAccount("1.1", a, active: false);
        a.UpdateParent(b.Id, UserId);
        var accounts = new[] { a, b };

        AccountHierarchyRules.HasActiveDescendant(a.Id, accounts).Should().BeFalse();
        AccountHierarchyRules.HasInactiveAncestor(a, accounts).Should().BeTrue();
        AccountHierarchyRules.HasInactiveAncestor(b, accounts).Should().BeFalse();
    }
}
