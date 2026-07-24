using ERP.Application.Common;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using ERP.Infrastructure.Modules.Purchases.PurchaseReception;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.Infrastructure.Tests.Modules.Purchases.PurchaseReception;

public sealed class PurchaseReceptionVerifierTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static PurchaseReceptionRecord Record(string ruc = "1791352688001", string accessKey = "0107202601179135268800120150270001617400016174011") =>
        new(2, PurchaseReceptionSourceDocType.Invoice, ruc, "QUALA ECUADOR S A", "015-027-000161740",
            accessKey, new DateOnly(2026, 7, 1), new DateTime(2026, 7, 1, 21, 6, 55), "0350016432",
            15.96m, 2.4m, 18.35m, null);

    private static (Mock<IBusinessPartnerRepository> bp, Mock<IBusinessPartnerRoleRepository> role,
        Mock<IPurchaseInvoiceRepository> purchase, Mock<ICurrentTenant> tenant) BuildMocks()
    {
        var bp = new Mock<IBusinessPartnerRepository>();
        var role = new Mock<IBusinessPartnerRoleRepository>();
        var purchase = new Mock<IPurchaseInvoiceRepository>();
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        return (bp, role, purchase, tenant);
    }

    [Fact]
    public async Task VerifyAsync_returns_NewSupplier_when_business_partner_does_not_exist()
    {
        var (bp, role, purchase, tenant) = BuildMocks();
        bp.Setup(r => r.GetByIdentificationAsync(TaxIdentification.SriRuc, "1791352688001", It.IsAny<CancellationToken>()))
            .ReturnsAsync((BusinessPartner?)null);

        var verifier = new PurchaseReceptionVerifier(bp.Object, role.Object, purchase.Object, tenant.Object);
        var result = await verifier.VerifyAsync([Record()]);

        result.Should().ContainSingle();
        result[0].SupplierExists.Should().BeFalse();
        result[0].PurchaseExists.Should().BeFalse();
        result[0].Status.Should().Be(PurchaseReceptionStatus.NewSupplier);
        purchase.Verify(r => r.GetByAccessKeyAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task VerifyAsync_returns_Pending_when_supplier_exists_but_purchase_does_not()
    {
        var (bp, role, purchase, tenant) = BuildMocks();
        var partner = BusinessPartner.Create(TenantId, TaxIdentification.SriRuc, "1791352688001", PersonType.Legal, "QUALA ECUADOR S A", UserId);
        bp.Setup(r => r.GetByIdentificationAsync(TaxIdentification.SriRuc, "1791352688001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);
        role.Setup(r => r.GetByTypeAsync(partner.Id, RoleType.Supplier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessPartnerRole.Create(TenantId, partner.Id, RoleType.Supplier, UserId));
        purchase.Setup(r => r.GetByAccessKeyAsync(TenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseInvoice?)null);

        var verifier = new PurchaseReceptionVerifier(bp.Object, role.Object, purchase.Object, tenant.Object);
        var result = await verifier.VerifyAsync([Record()]);

        result[0].SupplierExists.Should().BeTrue();
        result[0].PurchaseExists.Should().BeFalse();
        result[0].Status.Should().Be(PurchaseReceptionStatus.Pending);
    }

    [Fact]
    public async Task VerifyAsync_returns_Imported_when_supplier_and_purchase_both_exist()
    {
        var (bp, role, purchase, tenant) = BuildMocks();
        var partner = BusinessPartner.Create(TenantId, TaxIdentification.SriRuc, "1791352688001", PersonType.Legal, "QUALA ECUADOR S A", UserId);
        bp.Setup(r => r.GetByIdentificationAsync(TaxIdentification.SriRuc, "1791352688001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);
        role.Setup(r => r.GetByTypeAsync(partner.Id, RoleType.Supplier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessPartnerRole.Create(TenantId, partner.Id, RoleType.Supplier, UserId));

        var existingInvoice = PurchaseInvoice.CreateDraft(
            TenantId, Guid.NewGuid(), Guid.NewGuid(), partner.Id, "QUALA ECUADOR S A", "1791352688001",
            "01", "015-027-000161740", new DateOnly(2026, 7, 1), UserId, Guid.NewGuid(), "Contado", 1, 30,
            accessKey: "0107202601179135268800120150270001617400016174011");
        purchase.Setup(r => r.GetByAccessKeyAsync(TenantId, "0107202601179135268800120150270001617400016174011", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingInvoice);

        var verifier = new PurchaseReceptionVerifier(bp.Object, role.Object, purchase.Object, tenant.Object);
        var result = await verifier.VerifyAsync([Record()]);

        result[0].SupplierExists.Should().BeTrue();
        result[0].PurchaseExists.Should().BeTrue();
        result[0].Status.Should().Be(PurchaseReceptionStatus.Imported);
    }
}
