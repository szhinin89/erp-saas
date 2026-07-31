using ERP.Domain.Audit;
using ERP.Domain.Modules.Sales.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Sales;

public sealed class SalesReturnAuditTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SalesReturnId = Guid.NewGuid();
    private static readonly Guid SalesInvoiceId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static AuditActor Actor() =>
        new(TenantId, UserId, "tester", null, null, null, null, null, AuditSource.UserAction);

    [Fact]
    public void Create_con_datos_validos_pobla_los_campos_comunes_y_propios()
    {
        var audit = SalesReturnAudit.Create(
            Actor(),
            CompanyId,
            SalesReturnId,
            SalesInvoiceId,
            "DEV-000001",
            "Created",
            customerId: CustomerId,
            reason: "Producto en mal estado"
        );

        audit.TenantId.Should().Be(TenantId);
        audit.EntityId.Should().Be(SalesReturnId);
        audit.UserId.Should().Be(UserId);
        audit.Action.Should().Be("Created");
        audit.CompanyId.Should().Be(CompanyId);
        audit.SalesInvoiceId.Should().Be(SalesInvoiceId);
        audit.CustomerId.Should().Be(CustomerId);
        audit.ReturnNumber.Should().Be("DEV-000001");
        audit.GrandTotal.Should().BeNull();
        audit.Reason.Should().Be("Producto en mal estado");
    }

    [Fact]
    public void Create_para_Authorized_pobla_GrandTotal()
    {
        var audit = SalesReturnAudit.Create(
            Actor(),
            CompanyId,
            SalesReturnId,
            SalesInvoiceId,
            "DEV-000001",
            "Authorized",
            grandTotal: 23m
        );

        audit.GrandTotal.Should().Be(23m);
        audit.CustomerId.Should().BeNull();
    }

    [Fact]
    public void Create_rechaza_companyId_vacio()
    {
        var act = () =>
            SalesReturnAudit.Create(
                Actor(),
                Guid.Empty,
                SalesReturnId,
                SalesInvoiceId,
                "DEV-000001",
                "Created"
            );

        act.Should().Throw<ArgumentException>();
    }
}
