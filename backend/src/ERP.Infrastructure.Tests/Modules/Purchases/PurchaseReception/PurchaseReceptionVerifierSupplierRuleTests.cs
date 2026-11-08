using ERP.Application.Common;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using ERP.Infrastructure.Modules.Purchases.PurchaseReception;
using FluentAssertions;
using Moq;

namespace ERP.Infrastructure.Tests.Modules.Purchases.PurchaseReception;

/// <summary>
/// PURCHASES-RECEPTION-SUPPLIER-RULE-01 (H4) — misma regla para factura y NC: el proveedor está
/// identificado si socio y rol Proveedor son del tenant (activos o no), así que un documento histórico
/// ya registrado se reconoce aunque el proveedor esté hoy inactivo; el estado activo se informa aparte.
/// </summary>
public sealed class PurchaseReceptionVerifierSupplierRuleTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private const string Ruc = "1791111111001";
    private const string InvoiceKey = "0107202601179111111100120010010000000011234567811";
    private const string InvoiceNumber = "001-001-000000001";

    private readonly Mock<IBusinessPartnerRepository> _bp = new();
    private readonly Mock<IBusinessPartnerRoleRepository> _roles = new();
    private readonly Mock<IPurchaseInvoiceRepository> _purchases = new();

    private PurchaseReceptionVerifier Verifier() =>
        new(
            _bp.Object,
            _roles.Object,
            _purchases.Object,
            Mock.Of<IExpenseDocumentRepository>(),
            Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId)
        );

    private static PurchaseReceptionRecord Invoice() =>
        new(2, PurchaseReceptionSourceDocType.Invoice, Ruc, "PROVEEDOR S.A.", InvoiceNumber, InvoiceKey,
            new DateOnly(2026, 7, 1), new DateTime(2026, 7, 1, 10, 0, 0), "1790000000001",
            10m, 1.5m, 11.5m, null);

    private static PurchaseReceptionRecord CreditNote() =>
        new(3, PurchaseReceptionSourceDocType.CreditNote, Ruc, "PROVEEDOR S.A.", "001-001-000000009",
            "0207202604179111111100120010010000000091234567812", new DateOnly(2026, 7, 2),
            new DateTime(2026, 7, 2, 10, 0, 0), "1790000000001", 1m, 0.15m, 1.15m, InvoiceNumber);

    private BusinessPartner GivenSupplier(
        bool partnerActive = true,
        bool roleActive = true,
        bool hasRole = true,
        Guid? partnerTenant = null,
        Guid? roleTenant = null
    )
    {
        var partner = BusinessPartner.Create(
            partnerTenant ?? TenantId, TaxIdentification.SriRuc, Ruc, 2, "PROVEEDOR S.A.", UserId);
        if (!partnerActive)
            partner.Deactivate(UserId);
        _bp.Setup(r => r.GetByIdentificationAsync(TaxIdentification.SriRuc, Ruc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(partner);

        BusinessPartnerRole? role = null;
        if (hasRole)
        {
            role = BusinessPartnerRole.Create(roleTenant ?? TenantId, partner.Id, RoleType.Supplier, UserId);
            if (!roleActive)
                role.Revoke(UserId);
        }
        _roles.Setup(r => r.GetByTypeAsync(partner.Id, RoleType.Supplier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        return partner;
    }

    private static PurchaseInvoice Purchase(BusinessPartner partner) =>
        PurchaseInvoice.CreateDraft(
            TenantId, Guid.NewGuid(), Guid.NewGuid(), partner.Id, "PROVEEDOR S.A.", Ruc, "01",
            InvoiceNumber, new DateOnly(2026, 7, 1), UserId, Guid.NewGuid(), "Contado", 1, 30,
            accessKey: InvoiceKey);

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Historic_invoice_is_recognized_as_registered_with_an_inactive_supplier(
        bool partnerActive,
        bool roleActive
    )
    {
        var partner = GivenSupplier(partnerActive, roleActive);
        _purchases.Setup(r => r.GetByAccessKeyAsync(TenantId, InvoiceKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Purchase(partner));

        var item = (await Verifier().VerifyAsync([Invoice()])).Single();

        item.SupplierExists.Should().BeTrue();
        item.SupplierId.Should().Be(partner.Id);
        item.SupplierIsActive.Should().BeFalse();
        item.Status.Should().Be(PurchaseReceptionStatus.Imported);
        item.PurchaseState.Should().Be(PurchaseReceptionPurchaseState.Draft);
    }

    [Fact]
    public async Task Unregistered_invoice_with_inactive_supplier_is_pending_not_new_supplier()
    {
        GivenSupplier(partnerActive: false);

        var item = (await Verifier().VerifyAsync([Invoice()])).Single();

        item.SupplierExists.Should().BeTrue();
        item.SupplierIsActive.Should().BeFalse();
        item.Status.Should().Be(PurchaseReceptionStatus.Pending);
    }

    [Fact]
    public async Task Active_supplier_reports_active()
    {
        GivenSupplier();

        var item = (await Verifier().VerifyAsync([Invoice()])).Single();

        item.SupplierIsActive.Should().BeTrue();
        item.Status.Should().Be(PurchaseReceptionStatus.Pending);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Credit_note_with_inactive_supplier_resolves_the_linked_purchase(bool linked)
    {
        var partner = GivenSupplier(partnerActive: false);
        _purchases
            .Setup(r => r.GetBySupplierAndInvoiceNumberAsync(
                TenantId, partner.Id, InvoiceNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(linked ? Purchase(partner) : null);

        var item = (await Verifier().VerifyAsync([CreditNote()])).Single();

        item.SupplierExists.Should().BeTrue();
        item.SupplierIsActive.Should().BeFalse();
        item.AffectedPurchaseExists.Should().Be(linked);
    }

    [Theory]
    [InlineData(PurchaseReceptionSourceDocType.Invoice)]
    [InlineData(PurchaseReceptionSourceDocType.CreditNote)]
    public async Task Partner_without_supplier_role_is_a_new_supplier_for_both_document_types(
        PurchaseReceptionSourceDocType type
    )
    {
        GivenSupplier(hasRole: false);
        var record = type == PurchaseReceptionSourceDocType.Invoice ? Invoice() : CreditNote();

        var item = (await Verifier().VerifyAsync([record])).Single();

        item.SupplierExists.Should().BeFalse();
        item.SupplierIsActive.Should().BeNull();
        item.Status.Should().Be(PurchaseReceptionStatus.NewSupplier);
    }

    [Fact]
    public async Task Partner_or_role_of_another_tenant_is_never_recognized()
    {
        GivenSupplier(partnerTenant: Guid.NewGuid());
        var foreignPartner = (await Verifier().VerifyAsync([CreditNote()])).Single();

        GivenSupplier(roleTenant: Guid.NewGuid());
        var foreignRole = (await Verifier().VerifyAsync([Invoice()])).Single();

        foreignPartner.SupplierExists.Should().BeFalse();
        foreignRole.SupplierExists.Should().BeFalse();
        foreignRole.Status.Should().Be(PurchaseReceptionStatus.NewSupplier);
        _purchases.Verify(
            r => r.GetByAccessKeyAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
