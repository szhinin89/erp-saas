using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Purchases.Services;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// ADR-033, Fase 2 P1: PaymentTerm.IsActive debe validarse server-side al crear/editar un
/// borrador de Compras — hoy Compras resuelve el default del proveedor en backend
/// (SupplierRoleConfig.PaymentTermId), a diferencia de Ventas, pero tampoco validaba IsActive.
/// </summary>
public sealed class PurchasePaymentTermActiveGuardTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IPurchaseInvoiceRepository> Repo { get; } = new();
        public Mock<IBusinessPartnerRepository> BpRepo { get; } = new();
        public Mock<IBusinessPartnerRoleRepository> RoleRepo { get; } = new();
        public Mock<IPaymentTermRepository> PtRepo { get; } = new();
        public Mock<IItemRepository> ItemRepo { get; } = new();
        public Mock<IWarehouseRepository> WhRepo { get; } = new();
        public Mock<ISriTaxResolver> Tax { get; } = new();
        public Mock<IPurchaseReceptionDocumentRepository> ReceptionRepo { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ICurrentBranch> Branch { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();
        public Mock<IDatabaseExceptionTranslator> DbEx { get; } = new();

        public PaymentTerm DefaultPaymentTerm { get; }

        public Fixture()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
            Branch.Setup(b => b.BranchId).Returns(BranchId);
            User.Setup(u => u.UserId).Returns(UserId);

            var supplier = ERP.Domain.MasterData.Entities.BusinessPartner.Create(
                TenantId, "04", "1791352688001", 2, "Proveedor Demo", UserId
            );
            BpRepo
                .Setup(r => r.GetByIdAsync(SupplierId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(supplier);

            DefaultPaymentTerm = PaymentTerm.Create(TenantId, "CONT", "Contado", 1, 0, UserId);
            var role = ERP.Domain.MasterData.Entities.BusinessPartnerRole.Create(
                TenantId,
                SupplierId,
                RoleType.Supplier,
                UserId,
                SupplierRoleConfig.Create(DefaultPaymentTerm.Id)
            );
            RoleRepo
                .Setup(r => r.GetByTypeAsync(SupplierId, RoleType.Supplier, It.IsAny<CancellationToken>()))
                .ReturnsAsync(role);

            PtRepo
                .Setup(r => r.GetByIdAsync(TenantId, DefaultPaymentTerm.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(DefaultPaymentTerm);

            Tax.Setup(t => t.GetVatRateWithNameAsync("0", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ERP.Application.Common.Services.TaxRateResult(0m, "IVA 0%"));
        }

        public CreatePurchaseDraftHandler BuildCreateHandler() =>
            new(
                Repo.Object,
                BpRepo.Object,
                RoleRepo.Object,
                PtRepo.Object,
                ItemRepo.Object,
                WhRepo.Object,
                Tax.Object,
                ReceptionRepo.Object,
                Tenant.Object,
                Company.Object,
                Branch.Object,
                User.Object,
                DbEx.Object
            );

        public static CreatePurchaseDraftCommand ValidCommand(Guid? paymentTermId = null) =>
            new(
                SupplierId,
                "01",
                "001-001-000000001",
                DateOnly.FromDateTime(DateTime.UtcNow),
                new List<PurchaseLineInput> { new(null, "Servicio", 1m, 100m, "0") },
                PaymentTermId: paymentTermId
            );
    }

    [Fact]
    public async Task Rechaza_PaymentTermId_explicito_inactivo()
    {
        var f = new Fixture();
        var inactivePt = PaymentTerm.Create(TenantId, "30D", "30 días", 1, 30, UserId);
        inactivePt.Disable(UserId);
        f.PtRepo
            .Setup(r => r.GetByIdAsync(TenantId, inactivePt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactivePt);

        var handler = f.BuildCreateHandler();
        var result = await handler.Handle(
            Fixture.ValidCommand(inactivePt.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("inactiva");
        f.Repo.Verify(r => r.AddAsync(It.IsAny<PurchaseInvoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Rechaza_default_del_proveedor_si_quedo_inactivo()
    {
        var f = new Fixture();
        f.DefaultPaymentTerm.Disable(UserId);

        var handler = f.BuildCreateHandler();
        var result = await handler.Handle(Fixture.ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("inactiva");
        f.Repo.Verify(r => r.AddAsync(It.IsAny<PurchaseInvoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Acepta_condicion_activa_normalmente()
    {
        var f = new Fixture();
        var handler = f.BuildCreateHandler();

        var result = await handler.Handle(Fixture.ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        f.Repo.Verify(r => r.AddAsync(It.IsAny<PurchaseInvoice>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
