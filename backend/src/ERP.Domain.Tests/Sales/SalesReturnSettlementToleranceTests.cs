using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Sales;

/// <summary>
/// ACCOUNTING-10 — <see cref="SalesReturn.Authorize"/> NO usa tolerancia de liquidación: cada
/// asignación mueve dinero/crédito real (Caja/CxC) y el asiento exige Caja + CxC = GrandTotal, así
/// que Σ RefundAllocations debe igualar exactamente el total a 2 decimales.
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
    public void Authorize_asignacion_exacta_al_total_se_autoriza()
    {
        var ret = CreateDraftWithTotal(unitPrice: 4.00m, allocationAmount: 4.00m);

        ret.Authorize(UserId);

        ret.Status.Should().Be(SalesReturnStatus.Authorized);
    }

    [Fact]
    public void Authorize_diferencia_de_0_01_se_rechaza()
    {
        var ret = CreateDraftWithTotal(unitPrice: 4.00m, allocationAmount: 3.99m);

        var act = () => ret.Authorize(UserId);

        act.Should().Throw<DomainRuleViolationException>().WithMessage("*sumen exactamente el total*");
        ret.Status.Should().Be(SalesReturnStatus.Draft);
    }
}
