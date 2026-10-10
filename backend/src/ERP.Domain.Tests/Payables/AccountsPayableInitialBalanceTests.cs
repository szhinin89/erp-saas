using ERP.Domain.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Payables;

/// <summary>IL-6A — AccountsPayable con origen InitialBalance (saldo neto pendiente al corte, sin compra/gasto).</summary>
public sealed class AccountsPayableInitialBalanceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid BatchId = Guid.NewGuid();
    private static readonly Guid RowId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly Issue = new(2026, 8, 15);
    private static readonly DateOnly Due = new(2026, 10, 15);
    private static readonly DateOnly Cutoff = new(2026, 9, 30);

    private static AccountsPayable Create(
        string documentType = "01",
        string documentNumber = "001-001-000001234",
        DateOnly? issue = null,
        DateOnly? due = null,
        DateOnly? cutoff = null,
        decimal balance = 150.75m,
        Guid? batchId = null,
        Guid? rowId = null
    ) =>
        AccountsPayable.CreateInitialBalance(TenantId, CompanyId, BranchId, SupplierId, documentType, documentNumber,
            issue ?? Issue, due ?? Due, cutoff ?? Cutoff, balance, batchId ?? BatchId, rowId ?? RowId, UserId);

    [Fact]
    public void InitialBalance_se_persiste_como_3_al_final_del_enum()
    {
        // chk_accounts_payables_initial_balance_shape / uq_accounts_payables_initial_balance_document usan origin_type = 3.
        ((int)AccountsPayableOriginType.InitialBalance).Should().Be(3);
    }

    [Fact]
    public void Saldo_inicial_conserva_tipo_real_numero_normalizado_lote_y_fila_de_origen()
    {
        var p = Create(documentType: " 03 ", documentNumber: " 001-001-000001234 ");

        p.OriginType.Should().Be(AccountsPayableOriginType.InitialBalance);
        p.OriginId.Should().Be(RowId);
        p.ImportBatchId.Should().Be(BatchId);
        p.DocumentType.Should().Be("03");
        p.DocumentNumber.Should().Be("001-001-000001234");
        p.DocumentNumberNormalized.Should().Be("001001000001234");
        p.IssueDate.Should().Be(Issue);
        p.AccountingDate.Should().Be(Cutoff);
        p.TenantId.Should().Be(TenantId);
        p.CompanyId.Should().Be(CompanyId);
        p.BranchId.Should().Be(BranchId);
        p.SupplierId.Should().Be(SupplierId);
        p.Status.Should().Be(AccountsPayableStatus.Pending);
    }

    [Fact]
    public void Una_fila_es_una_cuota_por_el_saldo_neto_sin_pagos_ni_retenciones_historicas()
    {
        var p = Create(balance: 150.75m);

        p.Installments.Should().ContainSingle();
        var installment = p.Installments[0];
        installment.InstallmentNumber.Should().Be(1);
        installment.DueDate.Should().Be(Due);
        installment.Amount.Should().Be(150.75m);
        p.TotalAmount.Should().Be(150.75m);
        p.OutstandingAmount.Should().Be(150.75m);
        p.PaidAmount.Should().Be(0m);
        p.RetainedAmount.Should().Be(0m);
    }

    [Fact]
    public void Compra_y_gasto_no_llevan_datos_de_saldo_inicial()
    {
        var p = AccountsPayable.CreateFromOrigin(TenantId, CompanyId, BranchId, SupplierId,
            AccountsPayableOriginType.PurchaseInvoice, Guid.NewGuid(), "01", "001-001-1", Issue, Issue, UserId);

        p.DocumentNumberNormalized.Should().BeNull();
        p.ImportBatchId.Should().BeNull();
    }

    [Theory]
    [InlineData("", "001")]
    [InlineData("01", "")]
    [InlineData("010101", "001")]
    [InlineData("01", "---")]
    public void Tipo_o_numero_invalido_es_rechazado(string type, string number)
    {
        var act = () => Create(documentType: type, documentNumber: number);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Numero_de_mas_de_30_caracteres_es_rechazado()
    {
        var act = () => Create(documentNumber: new string('1', AccountsPayable.DocumentNumberMaxLen + 1));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Fechas_incoherentes_son_rechazadas()
    {
        ((Action)(() => Create(issue: new DateOnly(2026, 10, 1), due: new DateOnly(2026, 10, 5))))
            .Should().Throw<ArgumentException>().WithParameterName("issueDate");
        ((Action)(() => Create(due: new DateOnly(2026, 8, 14))))
            .Should().Throw<ArgumentException>().WithParameterName("dueDate");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10.123)]
    public void Saldo_no_positivo_o_con_mas_de_2_decimales_es_rechazado(decimal balance)
    {
        var act = () => Create(balance: balance);

        act.Should().Throw<ArgumentException>().WithParameterName("balance");
    }

    [Fact]
    public void Lote_y_fila_de_origen_son_obligatorios()
    {
        ((Action)(() => Create(batchId: Guid.Empty))).Should().Throw<ArgumentException>();
        ((Action)(() => Create(rowId: Guid.Empty))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Saldo_inicial_no_se_anula_con_la_anulacion_generica_de_compras_o_gastos()
    {
        var p = Create();

        var act = () => p.Cancel(UserId);

        act.Should().Throw<DomainRuleViolationException>();
        p.Status.Should().Be(AccountsPayableStatus.Pending);
    }

    [Fact]
    public void Saldo_inicial_admite_pagos_como_cualquier_CxP()
    {
        var p = Create(balance: 100m);

        p.RegisterPaymentToInstallment(p.Installments[0].Id, 40m, UserId);

        p.OutstandingAmount.Should().Be(60m);
        p.Status.Should().Be(AccountsPayableStatus.PartiallyPaid);
    }

    [Theory]
    [InlineData("001-001-000000123", "001001000000123")]
    [InlineData(" fac 12 ", "FAC12")]
    [InlineData(null, "")]
    public void Normalizacion_de_numero_es_unica_para_CxC_y_CxP(string? raw, string expected)
    {
        DocumentNumberKey.Normalize(raw).Should().Be(expected);
        ERP.Domain.Modules.Sales.Entities.SalesReceivable.NormalizeDocumentNumber(raw).Should().Be(expected);
    }
}
