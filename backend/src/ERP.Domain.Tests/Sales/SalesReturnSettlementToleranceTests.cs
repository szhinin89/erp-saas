using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Sales;

/// <summary>
/// SALES-INVOICE-FINAL-SEMANTIC-INTEGRITY-01 (Fase 6B) — <see cref="SalesReturn.Authorize"/> tenía
/// un segundo guard duplicado (allocationSum vs AuthorizedGrandTotal) con un umbral propio de
/// 0.01m, independiente del usado por <see cref="SalesInvoice.Authorize"/> — unificado en
/// <c>SalesSettlementPolicy.Tolerance</c> (0.02m) como única fuente de verdad.
/// </summary>
public sealed class SalesReturnSettlementToleranceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SalesInvoiceId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static SalesReturn CreateDraftWithTotal(decimal unitPrice, decimal allocationAmount)
    {
        var ret = SalesReturn.CreateDraft(
            TenantId,
            CompanyId,
            SalesInvoiceId,
            CustomerId,
            returnNumber: "DRAFT-RET-TOLERANCE",
            reason: "Prueba de tolerancia",
            createdBy: UserId
        );

        var line = SalesReturnDetail.Create(
            ret.Id,
            TenantId,
            Guid.NewGuid(),
            "Producto Test",
            quantity: 1m,
            unitPrice: unitPrice,
            discountPct: 0m,
            vatCode: "0",
            vatRate: 0m,
            uomCode: "UNIT"
        );
        ret.AddLine(line, UserId);

        var allocation = SalesReturnRefundAllocation.Create(
            ret.Id,
            TenantId,
            SalesReturnRefundMethod.Cash,
            allocationAmount
        );
        ret.AddRefundAllocation(allocation, UserId);

        return ret;
    }

    [Fact]
    public void Authorize_asignacion_dentro_de_tolerancia_0_02_se_autoriza()
    {
        var ret = CreateDraftWithTotal(unitPrice: 4.00m, allocationAmount: 3.99m);

        ret.Authorize(UserId);

        ret.Status.Should().Be(SalesReturnStatus.Authorized);
    }

    [Fact]
    public void Authorize_asignacion_fuera_de_tolerancia_0_02_lanza()
    {
        var ret = CreateDraftWithTotal(unitPrice: 4.00m, allocationAmount: 3.97m);

        var act = () => ret.Authorize(UserId);

        act.Should().Throw<InvalidOperationException>();
        ret.Status.Should().Be(SalesReturnStatus.Draft);
    }
}
