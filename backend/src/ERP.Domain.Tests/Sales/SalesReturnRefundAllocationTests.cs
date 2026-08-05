using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Sales;

public sealed class SalesReturnRefundAllocationTests
{
    private static readonly Guid ReturnId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public void Create_con_datos_validos_asigna_metodo_y_monto()
    {
        var allocation = SalesReturnRefundAllocation.Create(
            ReturnId,
            TenantId,
            SalesReturnRefundMethod.Cash,
            50m
        );

        allocation.ReturnId.Should().Be(ReturnId);
        allocation.Method.Should().Be(SalesReturnRefundMethod.Cash);
        allocation.Amount.Should().Be(50m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rechaza_monto_cero_o_negativo(decimal amount)
    {
        var act = () =>
            SalesReturnRefundAllocation.Create(
                ReturnId,
                TenantId,
                SalesReturnRefundMethod.ReceivableCredit,
                amount
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rechaza_metodo_de_reembolso_no_definido()
    {
        var act = () =>
            SalesReturnRefundAllocation.Create(
                ReturnId,
                TenantId,
                (SalesReturnRefundMethod)99,
                10m
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rechaza_devolucion_vacia()
    {
        var act = () =>
            SalesReturnRefundAllocation.Create(
                Guid.Empty,
                TenantId,
                SalesReturnRefundMethod.Cash,
                10m
            );

        act.Should().Throw<ArgumentException>();
    }
}
