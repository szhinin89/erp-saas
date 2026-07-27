using ERP.Domain.Modules.Sales.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Sales;

/// <summary>Fase 5.6.3 — SalesReceivable.RegisterCollection/ReverseCollection (Fase 5.5.5.2).</summary>
public sealed class SalesReceivableTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid InvoiceId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static SalesReceivable Create(decimal originalAmount = 100m) =>
        SalesReceivable.Create(TenantId, CompanyId, InvoiceId, CustomerId, originalAmount, UserId);

    [Fact]
    public void RegisterCollection_parcial_incrementa_PaidAmount_sin_saldar()
    {
        var receivable = Create(100m);

        receivable.RegisterCollection(40m, UserId);

        receivable.PaidAmount.Should().Be(40m);
        receivable.BalanceDue.Should().Be(60m);
    }

    [Fact]
    public void RegisterCollection_total_salda_el_saldo()
    {
        var receivable = Create(100m);

        receivable.RegisterCollection(100m, UserId);

        receivable.PaidAmount.Should().Be(100m);
        receivable.BalanceDue.Should().Be(0m);
    }

    [Fact]
    public void RegisterCollection_mayor_al_saldo_lanza_y_no_muta()
    {
        var receivable = Create(100m);
        receivable.RegisterCollection(60m, UserId);

        var act = () => receivable.RegisterCollection(60m, UserId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*excede el saldo pendiente*");
        receivable.PaidAmount.Should().Be(60m);
    }

    [Fact]
    public void RegisterCollection_rechaza_monto_cero_o_negativo()
    {
        var receivable = Create();

        var actZero = () => receivable.RegisterCollection(0m, UserId);
        var actNegative = () => receivable.RegisterCollection(-1m, UserId);

        actZero.Should().Throw<ArgumentException>();
        actNegative.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegisterCollection_sobre_documento_cancelado_lanza()
    {
        var receivable = Create(100m);
        receivable.Cancel(UserId);

        var act = () => receivable.RegisterCollection(10m, UserId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*cancelada*");
    }

    [Fact]
    public void ReverseCollection_correcto_decrementa_PaidAmount()
    {
        var receivable = Create(100m);
        receivable.RegisterCollection(60m, UserId);

        receivable.ReverseCollection(60m, UserId);

        receivable.PaidAmount.Should().Be(0m);
        receivable.BalanceDue.Should().Be(100m);
    }

    [Fact]
    public void ReverseCollection_mayor_al_cobrado_lanza()
    {
        var receivable = Create(100m);
        receivable.RegisterCollection(30m, UserId);

        var act = () => receivable.ReverseCollection(31m, UserId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*excede el monto cobrado*");
        receivable.PaidAmount.Should().Be(30m);
    }

    [Fact]
    public void ReverseCollection_rechaza_monto_cero_o_negativo()
    {
        var receivable = Create(100m);
        receivable.RegisterCollection(50m, UserId);

        var actZero = () => receivable.ReverseCollection(0m, UserId);
        var actNegative = () => receivable.ReverseCollection(-1m, UserId);

        actZero.Should().Throw<ArgumentException>();
        actNegative.Should().Throw<ArgumentException>();
    }
}
