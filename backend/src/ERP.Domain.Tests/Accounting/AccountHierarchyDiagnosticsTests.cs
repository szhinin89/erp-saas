using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Services;
using ERP.Domain.Modules.Accounting.ValueObjects;
using FluentAssertions;

namespace ERP.Domain.Tests.Accounting;

/// <summary>
/// ACCOUNTING-CHART-CANONICAL-HIERARCHY-01 Fase 1/8: invariantes del Plan de Cuentas canónico.
/// </summary>
public sealed class AccountHierarchyDiagnosticsTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();

    private static Account NewAccount(
        string code,
        Guid? parentId,
        bool allowsPosting = true,
        AccountType type = AccountType.Asset,
        AccountNature nature = AccountNature.Debit,
        bool active = true
    )
    {
        var account = Account.Create(
            TenantId,
            CompanyId,
            AccountCode.Create(code),
            $"Cuenta {code}",
            parentId,
            type,
            nature,
            allowsPosting,
            ActorId
        );
        if (!active)
            account.Disable(ActorId);
        return account;
    }

    [Fact]
    public void Jerarquia_consistente_no_produce_ningun_hallazgo()
    {
        var root = NewAccount("1", null, allowsPosting: false);
        var mid = NewAccount("1.1", root.Id, allowsPosting: false);
        var leaf = NewAccount("1.1.01", mid.Id, allowsPosting: true);

        var report = AccountHierarchyDiagnostics.Analyze([root, mid, leaf]);

        report.Issues.Should().BeEmpty();
        report.TotalAccounts.Should().Be(3);
    }

    [Fact]
    public void Codigo_raiz_con_ParentAccountId_es_OrphanRootWithParent()
    {
        var fakeParent = NewAccount("9", null, allowsPosting: false);
        var root = NewAccount("1", fakeParent.Id, allowsPosting: false);

        var report = AccountHierarchyDiagnostics.Analyze([fakeParent, root]);

        report.CountOf(AccountHierarchyIssueType.OrphanRootWithParent).Should().Be(1);
    }

    [Fact]
    public void Codigo_compuesto_sin_padre_existente_es_MissingImmediateParent()
    {
        var leaf = NewAccount("1.1.01.001", null, allowsPosting: true);

        var report = AccountHierarchyDiagnostics.Analyze([leaf]);

        report.CountOf(AccountHierarchyIssueType.MissingImmediateParent).Should().Be(1);
    }

    [Fact]
    public void ParentAccountId_que_no_coincide_con_el_prefijo_del_codigo_es_ParentMismatch()
    {
        var root = NewAccount("1", null, allowsPosting: false);
        var wrongParent = NewAccount("2", null, allowsPosting: false);
        // "1.1" implica padre "1" por código, pero apunta a "2".
        var mid = NewAccount("1.1", wrongParent.Id, allowsPosting: false);

        var report = AccountHierarchyDiagnostics.Analyze([root, wrongParent, mid]);

        report.CountOf(AccountHierarchyIssueType.ParentMismatch).Should().Be(1);
    }

    [Fact]
    public void Profundidad_por_ParentAccountId_distinta_de_profundidad_por_codigo_es_LevelMismatch()
    {
        var root = NewAccount("1", null, allowsPosting: false);
        // Salta el nivel intermedio "1.1": "1.1.01" cuelga directo de "1" (profundidad código 2,
        // profundidad por ParentAccountId 1).
        var leaf = NewAccount("1.1.01", root.Id, allowsPosting: true);

        var report = AccountHierarchyDiagnostics.Analyze([root, leaf]);

        report.CountOf(AccountHierarchyIssueType.LevelMismatch).Should().Be(1);
        // También dispara MissingImmediateParent porque "1.1" no existe.
        report.CountOf(AccountHierarchyIssueType.MissingImmediateParent).Should().Be(1);
    }

    [Fact]
    public void Cuenta_con_hijas_y_AllowsPosting_true_es_ParentAllowsPostingWithChildren()
    {
        var root = NewAccount("1", null, allowsPosting: true);
        var child = NewAccount("1.1", root.Id, allowsPosting: true);

        var report = AccountHierarchyDiagnostics.Analyze([root, child]);

        report.CountOf(AccountHierarchyIssueType.ParentAllowsPostingWithChildren).Should().Be(1);
    }

    [Fact]
    public void Ciclo_en_la_cadena_de_padres_es_CycleDetected()
    {
        var a = NewAccount("1", null, allowsPosting: false);
        var b = NewAccount("1.1", a.Id, allowsPosting: false);
        // Fuerza un ciclo a->b->a reasignando el padre de "a" a "b" (solo posible en test, la
        // Application real lo bloquea vía CreatesCycle en UpdateAccountHandler).
        a.UpdateParent(b.Id, ActorId);

        var report = AccountHierarchyDiagnostics.Analyze([a, b]);

        report.CountOf(AccountHierarchyIssueType.CycleDetected).Should().BeGreaterThan(0);
    }

    [Fact]
    public void Cuenta_referenciada_por_posting_rule_que_no_es_hoja_posteable_activa_es_invalida()
    {
        var root = NewAccount("1", null, allowsPosting: false);
        var leafInactive = NewAccount("1.1", root.Id, allowsPosting: true, active: false);

        var rule = PostingRule.Create(
            TenantId,
            CompanyId,
            "Sales",
            "InvoiceIssued",
            debitAccountId: null,
            creditAccountId: null,
            taxCode: null,
            createdBy: ActorId
        );
        rule.AddLine(leafInactive.Id, AccountNature.Debit, PostingAmountKind.GrandTotal);

        var report = AccountHierarchyDiagnostics.Analyze([root, leafInactive], [rule]);

        report.CountOf(AccountHierarchyIssueType.PostingRuleAccountInvalid).Should().Be(1);
    }

    [Fact]
    public void Cuenta_referenciada_por_posting_rule_hoja_activa_posteable_no_produce_hallazgo()
    {
        var root = NewAccount("1", null, allowsPosting: false);
        var leaf = NewAccount("1.1", root.Id, allowsPosting: true);

        var rule = PostingRule.Create(
            TenantId,
            CompanyId,
            "Sales",
            "InvoiceIssued",
            debitAccountId: null,
            creditAccountId: null,
            taxCode: null,
            createdBy: ActorId
        );
        rule.AddLine(leaf.Id, AccountNature.Debit, PostingAmountKind.GrandTotal);

        var report = AccountHierarchyDiagnostics.Analyze([root, leaf], [rule]);

        report.CountOf(AccountHierarchyIssueType.PostingRuleAccountInvalid).Should().Be(0);
    }
}
