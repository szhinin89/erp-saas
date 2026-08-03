using ERP.Domain.Audit;
using ERP.Domain.Modules.Purchases.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Purchases;

public sealed class PurchaseReturnAuditTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid PurchaseReturnId = Guid.NewGuid();
    private static readonly Guid PurchaseInvoiceId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static AuditActor Actor() =>
        new(TenantId, UserId, "tester", null, null, null, null, null, AuditSource.UserAction);

    [Fact]
    public void Create_con_datos_validos_pobla_los_campos_comunes_y_propios_incluido_BranchId()
    {
        var audit = PurchaseReturnAudit.Create(
            Actor(),
            CompanyId,
            BranchId,
            PurchaseReturnId,
            PurchaseInvoiceId,
            "Created",
            supplierId: SupplierId,
            returnNumber: "00000001",
            reason: "Producto en mal estado"
        );

        audit.TenantId.Should().Be(TenantId);
        audit.EntityId.Should().Be(PurchaseReturnId);
        audit.UserId.Should().Be(UserId);
        audit.Action.Should().Be("Created");
        audit.CompanyId.Should().Be(CompanyId);
        audit.BranchId.Should().Be(BranchId);
        audit.PurchaseInvoiceId.Should().Be(PurchaseInvoiceId);
        audit.SupplierId.Should().Be(SupplierId);
        audit.ReturnNumber.Should().Be("00000001");
        audit.GrandTotal.Should().BeNull();
        audit.Reason.Should().Be("Producto en mal estado");
    }

    [Fact]
    public void Create_conserva_exactamente_el_BranchId_recibido_sin_sustituirlo()
    {
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();

        var auditA = PurchaseReturnAudit.Create(
            Actor(),
            CompanyId,
            branchA,
            PurchaseReturnId,
            PurchaseInvoiceId,
            "Created"
        );
        var auditB = PurchaseReturnAudit.Create(
            Actor(),
            CompanyId,
            branchB,
            PurchaseReturnId,
            PurchaseInvoiceId,
            "Created"
        );

        auditA.BranchId.Should().Be(branchA);
        auditB.BranchId.Should().Be(branchB);
        auditA.BranchId.Should().NotBe(auditB.BranchId);
        auditA.BranchId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_para_Authorized_pobla_GrandTotal_y_conserva_BranchId()
    {
        var audit = PurchaseReturnAudit.Create(
            Actor(),
            CompanyId,
            BranchId,
            PurchaseReturnId,
            PurchaseInvoiceId,
            "Authorized",
            returnNumber: "00000001",
            grandTotal: 23m
        );

        audit.GrandTotal.Should().Be(23m);
        audit.BranchId.Should().Be(BranchId);
    }

    [Fact]
    public void Create_rechaza_companyId_vacio()
    {
        var act = () =>
            PurchaseReturnAudit.Create(
                Actor(),
                Guid.Empty,
                BranchId,
                PurchaseReturnId,
                PurchaseInvoiceId,
                "Created"
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rechaza_BranchId_vacio_no_lo_sustituye_por_GuidEmpty_silenciosamente()
    {
        var act = () =>
            PurchaseReturnAudit.Create(
                Actor(),
                CompanyId,
                Guid.Empty,
                PurchaseReturnId,
                PurchaseInvoiceId,
                "Created"
            );

        act.Should().Throw<ArgumentException>().WithParameterName("branchId");
    }
}
