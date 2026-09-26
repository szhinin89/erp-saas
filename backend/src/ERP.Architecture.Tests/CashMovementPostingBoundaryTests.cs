using FluentAssertions;
using NetArchTest.Rules;

namespace ERP.Architecture.Tests;

/// <summary>
/// ZH-SUPPLIER-PAYMENT-CASH-TRANSFER-HARDENING-02A — <c>CashMovement</c> es exclusivamente el
/// efecto operativo de caja; el asiento lo genera siempre el documento financiero de origen
/// (<c>SupplierPayment</c>, <c>SalesInvoice</c>, <c>SupplierCreditRefundTransaction</c>…). Si algún
/// componente de Posting consumiera <c>CashMovement</c>/<c>CashSession</c>, un pago en efectivo se
/// contabilizaría dos veces (documento + movimiento). Este gate lo impide.
/// </summary>
public sealed class CashMovementPostingBoundaryTests
{
    private static readonly System.Reflection.Assembly ApplicationAssembly =
        typeof(ERP.Application.DependencyInjection).Assembly;

    [Fact]
    public void Posting_no_depende_de_CashMovement_ni_de_CashSession()
    {
        var result = Types
            .InAssembly(ApplicationAssembly)
            .That()
            .ResideInNamespaceStartingWith("ERP.Application.Modules.Accounting.Posting")
            .ShouldNot()
            .HaveDependencyOnAny(
                "ERP.Domain.Modules.Caja.Entities.CashMovement",
                "ERP.Domain.Modules.Caja.Entities.CashSession"
            )
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }
}
