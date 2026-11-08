using ERP.Application.Modules.Purchases.PurchaseReception.Services;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases.PurchaseReception;

/// <summary>
/// PURCHASES-RECEPTION-SUPPLIER-RULE-01 (H4) — una operación NUEVA desde recepción (borrador de compra o
/// de gasto) exige socio activo y rol Proveedor activo del tenant, y rechaza sin escribir.
/// </summary>
public sealed class ReceptionSupplierResolverTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private const string SupplierRuc = "1791111111001";

    private readonly Mock<IBusinessPartnerRepository> _partners = new();
    private readonly Mock<IBusinessPartnerRoleRepository> _roles = new();
    private readonly Mock<IPurchaseReceptionDocumentRepository> _documents = new();
    private readonly BusinessPartner _supplier =
        BusinessPartner.Create(TenantId, TaxIdentification.SriRuc, SupplierRuc, 2, "PROVEEDOR S.A.", UserId);

    private static PurchaseReceptionDocument Document() =>
        PurchaseReceptionDocument.Create(
            TenantId, Guid.NewGuid(), Guid.NewGuid(), PurchaseReceptionSourceDocType.Invoice,
            SupplierRuc, "PROVEEDOR S.A.", supplierId: null, new string('3', 49), "001-001-000000001",
            new DateOnly(2026, 7, 1), new DateTime(2026, 7, 1, 15, 0, 0, DateTimeKind.Utc),
            10m, 1.5m, 11.5m, UserId);

    private void GivenRole(BusinessPartnerRole? role)
    {
        _partners
            .Setup(r => r.GetByIdentificationAsync(TaxIdentification.SriRuc, SupplierRuc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_supplier);
        _roles
            .Setup(r => r.GetByTypeAsync(_supplier.Id, RoleType.Supplier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
    }

    private BusinessPartnerRole Role(Guid? tenantId = null) =>
        BusinessPartnerRole.Create(tenantId ?? TenantId, _supplier.Id, RoleType.Supplier, UserId,
            SupplierRoleConfig.Create());

    private Task<ERP.Application.Common.Result<BusinessPartner>> Resolve(PurchaseReceptionDocument document) =>
        ReceptionSupplierResolver.ResolveAsync(
            document, _partners.Object, _roles.Object, _documents.Object, TenantId, UserId, CancellationToken.None);

    [Fact]
    public async Task Active_partner_and_role_resolve_and_assign_the_supplier()
    {
        GivenRole(Role());
        var document = Document();

        var result = await Resolve(document);

        result.IsSuccess.Should().BeTrue();
        document.SupplierId.Should().Be(_supplier.Id);
        _documents.Verify(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Inactive_partner_is_rejected_without_writes()
    {
        GivenRole(Role());
        _supplier.Deactivate(UserId);
        var document = Document();

        var result = await Resolve(document);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("inactivo");
        document.SupplierId.Should().BeNull();
        _documents.Verify(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Revoked_supplier_role_is_rejected_without_writes()
    {
        var role = Role();
        role.Revoke(UserId);
        GivenRole(role);
        var document = Document();

        var result = await Resolve(document);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ReceptionSupplierResolver.InactiveSupplierRoleMessage);
        document.SupplierId.Should().BeNull();
        _documents.Verify(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Missing_role_or_role_of_another_tenant_is_rejected()
    {
        GivenRole(null);
        (await Resolve(Document())).IsSuccess.Should().BeFalse();

        GivenRole(Role(Guid.NewGuid()));
        (await Resolve(Document())).Error.Should().Be(ReceptionSupplierResolver.InactiveSupplierRoleMessage);

        _documents.Verify(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
