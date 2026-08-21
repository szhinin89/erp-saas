using ERP.Application.Common;
using ERP.Application.Modules.Caja.UseCases;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Caja;

/// <summary>
/// ERP-CORE-CLOSEOUT-05-FIX01 (P0-4) — CreateCashRegisterHandler validaba que la sucursal exista y
/// esté activa, pero nunca comparaba <c>branch.CompanyId</c> contra la empresa actual. Un usuario
/// de la Empresa A podía crear una Caja apuntando a una sucursal de la Empresa B del mismo tenant.
/// </summary>
public sealed class CreateCashRegisterBranchOwnershipTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyAId = Guid.NewGuid();
    private static readonly Guid CompanyBId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static Branch CreateBranch(Guid companyId) =>
        Branch.Create(
            tenantId: TenantId,
            name: "Sucursal Test",
            address: "Av. Test 100",
            code: "BT",
            description: null,
            reference: null,
            postalCode: null,
            phone: null,
            secondaryPhone: null,
            email: null,
            website: null,
            managerName: null,
            managerPosition: null,
            managerEmail: null,
            managerPhone: null,
            countryId: null,
            provinceId: null,
            cantonId: null,
            parishId: null,
            latitude: null,
            longitude: null,
            openingDate: null,
            internalNotes: null,
            isMainBranch: true,
            createdBy: UserId,
            companyId: companyId
        );

    private sealed class Fixture
    {
        public Mock<ICashRegisterRepository> Repo { get; } = new();
        public Mock<IEmissionPointRepository> EmissionPointRepo { get; } = new();
        public Mock<IBranchRepository> BranchRepo { get; } = new();
        public Mock<IWarehouseRepository> WarehouseRepo { get; } = new();
        public Mock<IBusinessPartnerRepository> CustomerRepo { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();

        public Fixture()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyAId);
            User.Setup(u => u.UserId).Returns(UserId);
            Repo.Setup(r =>
                    r.ExistsByCodeAsync(
                        TenantId,
                        It.IsAny<Guid>(),
                        It.IsAny<string>(),
                        null,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(false);
        }

        public CreateCashRegisterHandler BuildHandler() =>
            new(
                Repo.Object,
                EmissionPointRepo.Object,
                BranchRepo.Object,
                WarehouseRepo.Object,
                CustomerRepo.Object,
                Tenant.Object,
                Company.Object,
                User.Object
            );
    }

    private static CreateCashRegisterCommand BuildCommand(Guid branchId) =>
        new(branchId, "CAJA-01", "Caja Principal", EmissionPointId: null);

    [Fact]
    public async Task Crear_caja_con_sucursal_de_otra_empresa_es_rechazado()
    {
        var branchOfCompanyB = CreateBranch(CompanyBId);
        var f = new Fixture();
        f.BranchRepo.Setup(r => r.GetByIdAsync(TenantId, branchOfCompanyB.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(branchOfCompanyB);

        var result = await f.BuildHandler()
            .Handle(BuildCommand(branchOfCompanyB.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        f.Repo.Verify(r => r.AddAsync(It.IsAny<CashRegister>(), It.IsAny<CancellationToken>()), Times.Never);
        f.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Crear_caja_con_sucursal_de_la_propia_empresa_funciona()
    {
        var ownBranch = CreateBranch(CompanyAId);
        var f = new Fixture();
        f.BranchRepo.Setup(r => r.GetByIdAsync(TenantId, ownBranch.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ownBranch);

        CashRegister? captured = null;
        f.Repo.Setup(r => r.AddAsync(It.IsAny<CashRegister>(), It.IsAny<CancellationToken>()))
            .Callback<CashRegister, CancellationToken>((r, _) =>
            {
                // EF hidrata la navegación Branch al recargar; en el test la fijamos por
                // reflexión (setter privado) para poder ejercer CajaMapper.ToDto sin un DbContext real.
                typeof(CashRegister)
                    .GetProperty(nameof(CashRegister.Branch))!
                    .SetValue(r, ownBranch);
                captured = r;
            })
            .Returns(Task.CompletedTask);
        f.Repo.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => captured);

        var result = await f.BuildHandler()
            .Handle(BuildCommand(ownBranch.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        f.Repo.Verify(r => r.AddAsync(It.IsAny<CashRegister>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
