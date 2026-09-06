using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.MasterData.Services;
using ERP.Application.Modules.Purchases.Services;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// ADR-033: Fase 2 P1 estableció la validación server-side de PaymentTerm.IsActive; Fase 3b
/// centralizó la resolución del default en IPaymentTermDefaultResolver (mockeado aquí — la
/// mecánica de resolución en sí, incluido el fallback a CompanyBpPurchaseSettings, se prueba en
/// PaymentTermDefaultResolverTests). Este archivo verifica que CreatePurchaseDraftHandler
/// propaga correctamente el Result del resolver, sin volver a implementar la cadena.
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
        public Mock<IPaymentTermDefaultResolver> PtResolver { get; } = new();
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

            var supplier = BusinessPartner.Create(
                TenantId, "04", "1791352688001", 2, "Proveedor Demo", UserId
            );
            BpRepo
                .Setup(r => r.GetByIdAsync(SupplierId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(supplier);

            DefaultPaymentTerm = PaymentTerm.Create(TenantId, "CONT", "Contado", 1, 0, UserId);

            // El proveedor necesita config SRI (BusinessPartnerRole.SupplierConfig) para pasar el
            // guard previo al de PaymentTerm — no relacionado con la resolución del default.
            var role = BusinessPartnerRole.Create(
                TenantId, SupplierId, ERP.Domain.MasterData.Enums.RoleType.Supplier, UserId,
                ERP.Domain.MasterData.ValueObjects.SupplierRoleConfig.Create(DefaultPaymentTerm.Id)
            );
            RoleRepo
                .Setup(r => r.GetByTypeAsync(SupplierId, ERP.Domain.MasterData.Enums.RoleType.Supplier, It.IsAny<CancellationToken>()))
                .ReturnsAsync(role);

            // Default por defecto del fixture: el resolver siempre devuelve la condición activa
            // (explícita o implícita) salvo que un test la sobreescriba.
            PtResolver
                .Setup(r => r.ResolveForPurchaseAsync(SupplierId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<PaymentTerm>.Success(DefaultPaymentTerm));

            Tax.Setup(t => t.GetVatRateWithNameAsync("0", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ERP.Application.Common.Services.TaxRateResult(0m, "IVA 0%"));
        }

        public CreatePurchaseDraftHandler BuildCreateHandler() =>
            new(
                Repo.Object,
                BpRepo.Object,
                RoleRepo.Object,
                PtResolver.Object,
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
        var inactivePtId = Guid.NewGuid();
        f.PtResolver
            .Setup(r => r.ResolveForPurchaseAsync(SupplierId, inactivePtId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentTerm>.ValidationFailure("La condición de pago se encuentra inactiva."));

        var handler = f.BuildCreateHandler();
        var result = await handler.Handle(
            Fixture.ValidCommand(inactivePtId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("inactiva");
        f.Repo.Verify(r => r.AddAsync(It.IsAny<PurchaseInvoice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Rechaza_cuando_el_resolver_no_encuentra_default_valido()
    {
        var f = new Fixture();
        f.PtResolver
            .Setup(r => r.ResolveForPurchaseAsync(SupplierId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaymentTerm>.ValidationFailure(
                "Debe seleccionar una condición de pago; este proveedor no tiene una configurada para esta empresa."
            ));

        var handler = f.BuildCreateHandler();
        var result = await handler.Handle(Fixture.ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Debe seleccionar");
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
