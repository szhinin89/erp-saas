using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Sales;

/// <summary>IL-5A — SalesReceivable con origen InitialBalance (saldo pendiente al corte, sin factura).</summary>
public sealed class SalesReceivableInitialBalanceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid BatchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly Issue = new(2026, 8, 15);
    private static readonly DateOnly Due = new(2026, 10, 15);

    private static SalesReceivable Create(
        string documentNumber = "001-001-000001234",
        DateOnly? issue = null,
        DateOnly? due = null,
        decimal balance = 150.75m
    ) =>
        SalesReceivable.CreateInitialBalance(TenantId, CompanyId, BranchId, CustomerId, documentNumber,
            issue ?? Issue, due ?? Due, balance, BatchId, UserId);

    [Fact]
    public void Saldo_inicial_no_tiene_factura_y_conserva_sus_datos_propios()
    {
        var r = Create();

        r.Origin.Should().Be(SalesReceivableOrigin.InitialBalance);
        r.InvoiceId.Should().BeNull();
        r.DocumentNumber.Should().Be("001-001-000001234");
        r.DocumentNumberNormalized.Should().Be("001001000001234");
        r.IssueDate.Should().Be(Issue);
        r.BranchId.Should().Be(BranchId);
        r.ImportBatchId.Should().Be(BatchId);
        r.CustomerId.Should().Be(CustomerId);
        r.Status.Should().Be("pending");
    }

    [Fact]
    public void El_monto_original_es_el_saldo_pendiente_sin_cobros_historicos()
    {
        var r = Create(balance: 150.75m);

        r.OriginalAmount.Should().Be(150.75m);
        r.PaidAmount.Should().Be(0m);
        r.BalanceDue.Should().Be(150.75m);
    }

    [Fact]
    public void Genera_una_unica_cuota_por_el_saldo_con_su_vencimiento()
    {
        var r = Create();

        r.Installments.Should().ContainSingle();
        var installment = r.Installments[0];
        installment.InstallmentNumber.Should().Be(1);
        installment.DueDate.Should().Be(Due);
        installment.Amount.Should().Be(150.75m);
        installment.ReceivableId.Should().Be(r.Id);
    }

    [Fact]
    public void Vencimiento_igual_a_la_emision_es_valido()
    {
        var r = Create(issue: Issue, due: Issue);

        r.Installments[0].DueDate.Should().Be(Issue);
    }

    [Fact]
    public void Vencimiento_anterior_a_la_emision_se_rechaza()
    {
        var act = () => Create(issue: Issue, due: Issue.AddDays(-1));

        act.Should().Throw<ArgumentException>().WithMessage("*vencimiento*anterior*emisión*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Saldo_no_positivo_se_rechaza(decimal balance)
    {
        var act = () => Create(balance: balance);

        act.Should().Throw<ArgumentException>().WithMessage("*mayor a cero*");
    }

    [Fact]
    public void Saldo_con_mas_de_dos_decimales_se_rechaza_sin_redondear()
    {
        var act = () => Create(balance: 10.555m);

        act.Should().Throw<ArgumentException>().WithMessage("*2 decimales*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("---")]
    public void Numero_de_documento_vacio_o_sin_letras_ni_digitos_se_rechaza(string number)
    {
        var act = () => Create(documentNumber: number);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Numero_de_documento_demasiado_largo_se_rechaza()
    {
        var act = () => Create(documentNumber: new string('9', SalesReceivable.DocumentNumberMaxLen + 1));

        act.Should().Throw<ArgumentException>().WithMessage("*no puede superar*");
    }

    [Fact]
    public void Numero_de_documento_se_guarda_sin_espacios_externos()
    {
        Create(documentNumber: "  FAC-99  ").DocumentNumber.Should().Be("FAC-99");
    }

    [Theory]
    [InlineData("001-001-000001234", "001001000001234")]
    [InlineData(" fac 0099 ", "FAC0099")]
    [InlineData("A/b.1", "AB1")]
    [InlineData(null, "")]
    public void Normalizacion_del_numero_ignora_separadores_y_mayusculas(string? raw, string expected)
    {
        SalesReceivable.NormalizeDocumentNumber(raw).Should().Be(expected);
    }

    [Fact]
    public void Sucursal_cliente_y_lote_son_obligatorios()
    {
        ((Action)(() => SalesReceivable.CreateInitialBalance(TenantId, CompanyId, Guid.Empty, CustomerId, "1", Issue,
            Due, 1m, BatchId, UserId))).Should().Throw<ArgumentException>();
        ((Action)(() => SalesReceivable.CreateInitialBalance(TenantId, CompanyId, BranchId, Guid.Empty, "1", Issue,
            Due, 1m, BatchId, UserId))).Should().Throw<ArgumentException>();
        ((Action)(() => SalesReceivable.CreateInitialBalance(TenantId, CompanyId, BranchId, CustomerId, "1", Issue,
            Due, 1m, Guid.Empty, UserId))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Saldo_inicial_no_usa_la_cancelacion_generica_de_facturas()
    {
        var r = Create();

        var act = () => r.Cancel(UserId);

        act.Should().Throw<DomainRuleViolationException>();
        r.Status.Should().Be("pending");
        r.Installments.Should().ContainSingle();
    }

    [Fact]
    public void Una_cxc_de_factura_conserva_origen_Invoice_y_sin_datos_de_saldo_inicial()
    {
        var r = SalesReceivable.Create(TenantId, CompanyId, Guid.NewGuid(), CustomerId, 100m, UserId);

        r.Origin.Should().Be(SalesReceivableOrigin.Invoice);
        r.InvoiceId.Should().NotBeNull();
        r.DocumentNumber.Should().BeNull();
        r.DocumentNumberNormalized.Should().BeNull();
        r.IssueDate.Should().BeNull();
        r.BranchId.Should().BeNull();
        r.ImportBatchId.Should().BeNull();
    }

    [Fact]
    public void El_saldo_inicial_acepta_cobros_como_cualquier_cxc()
    {
        var r = Create(balance: 100m);

        r.RegisterCollection(40m, UserId);

        r.BalanceDue.Should().Be(60m);
    }
}
