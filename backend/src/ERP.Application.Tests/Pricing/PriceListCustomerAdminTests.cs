using ERP.Application.Common;
using ERP.Application.Modules.Pricing.DTOs;
using ERP.Application.Modules.Pricing.UseCases.PriceListCustomers;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.Models;
using ERP.Domain.Modules.Pricing.Entities;
using ERP.Domain.Modules.Pricing.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Pricing;

/// <summary>
/// PRICING-CUSTOMER-PRICE-LIST-ADMIN-05B — administración de PriceListCustomer desde
/// /products/pricing. Cubre: asignar, listar, desactivar/reactivar, mover A→B (conflicto →
/// confirmación → switch), idempotencia, cliente inexistente y aislamiento de tenant/company.
/// La transacción atómica del switch (rollback real si falla) se prueba en
/// PriceListCustomerRepositoryIntegrationTests (Postgres real) — un mock no puede probar
/// atomicidad de BD. NO integrado con Sales.
/// </summary>
public sealed class PriceListCustomerAdminTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IPriceListCustomerRepository> Assignments { get; } = new();
        public Mock<IPriceListRepository> PriceLists { get; } = new();
        public Mock<IBusinessPartnerRepository> BusinessPartners { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();

        public Fixture()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
            User.Setup(u => u.UserId).Returns(UserId);
        }

        public AssignCustomerToPriceListHandler BuildAssignHandler() =>
            new(Assignments.Object, PriceLists.Object, BusinessPartners.Object, Tenant.Object, Company.Object, User.Object);

        public DisablePriceListCustomerHandler BuildDisableHandler() =>
            new(Assignments.Object, Tenant.Object, User.Object);

        public GetPriceListCustomersHandler BuildListHandler() =>
            new(Assignments.Object, PriceLists.Object, BusinessPartners.Object, Tenant.Object);
    }

    private static PriceList CreateList(string code) =>
        PriceList.Create(TenantId, CompanyId, code, $"Lista {code}", "USD", isDefault: false, createdBy: UserId);

    private static BusinessPartner CreateCustomer() =>
        BusinessPartner.Create(TenantId, "05", "1710034065", null, "Cliente de prueba", UserId);

    [Fact]
    public async Task Asignar_cliente_sin_lista_previa_devuelve_Assigned()
    {
        var f = new Fixture();
        var list = CreateList("A");
        var customer = CreateCustomer();
        f.PriceLists.Setup(r => r.GetByIdAsync(TenantId, list.Id, It.IsAny<CancellationToken>())).ReturnsAsync(list);
        f.BusinessPartners.Setup(r => r.GetByIdAsync(CustomerId, It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        f.Assignments
            .Setup(r => r.GetByCustomerAsync(TenantId, CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PriceListCustomer>());

        var result = await f.BuildAssignHandler().Handle(
            new AssignCustomerToPriceListCommand(list.Id, CustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Status.Should().Be(PriceListCustomerAssignStatus.Assigned);
        f.Assignments.Verify(r => r.AddAsync(It.IsAny<PriceListCustomer>(), It.IsAny<CancellationToken>()), Times.Once);
        f.Assignments.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Listar_clientes_asignados_devuelve_nombre_e_identificacion()
    {
        var f = new Fixture();
        var list = CreateList("A");
        f.PriceLists.Setup(r => r.GetByIdAsync(TenantId, list.Id, It.IsAny<CancellationToken>())).ReturnsAsync(list);
        var assignment = PriceListCustomer.Create(TenantId, CompanyId, list.Id, CustomerId, UserId);
        f.Assignments
            .Setup(r => r.GetByPriceListAsync(TenantId, list.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { assignment });
        f.BusinessPartners
            .Setup(r => r.GetDisplayInfoByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, BusinessPartnerDisplayInfo>
            {
                [CustomerId] = new(CustomerId, "Cliente VIP", null, "1710034065"),
            });

        var result = await f.BuildListHandler().Handle(new GetPriceListCustomersQuery(list.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var row = result.Value!.Single();
        row.CustomerId.Should().Be(CustomerId);
        row.CustomerName.Should().Be("Cliente VIP");
        row.CustomerIdentificationNumber.Should().Be("1710034065");
    }

    [Fact]
    public async Task Desactivar_asignacion_activa_la_deshabilita()
    {
        var f = new Fixture();
        var list = CreateList("A");
        var assignment = PriceListCustomer.Create(TenantId, CompanyId, list.Id, CustomerId, UserId);
        f.Assignments
            .Setup(r => r.FindByKeyAsync(TenantId, list.Id, CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assignment);

        var result = await f.BuildDisableHandler().Handle(
            new DisablePriceListCustomerCommand(list.Id, CustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        assignment.IsActive.Should().BeFalse();
        f.Assignments.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reactivar_relacion_existente_deshabilitada_para_la_misma_lista()
    {
        var f = new Fixture();
        var list = CreateList("A");
        var customer = CreateCustomer();
        var assignment = PriceListCustomer.Create(TenantId, CompanyId, list.Id, CustomerId, UserId);
        assignment.Disable(UserId);
        f.PriceLists.Setup(r => r.GetByIdAsync(TenantId, list.Id, It.IsAny<CancellationToken>())).ReturnsAsync(list);
        f.BusinessPartners.Setup(r => r.GetByIdAsync(CustomerId, It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        f.Assignments
            .Setup(r => r.GetByCustomerAsync(TenantId, CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { assignment });

        var result = await f.BuildAssignHandler().Handle(
            new AssignCustomerToPriceListCommand(list.Id, CustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Status.Should().Be(PriceListCustomerAssignStatus.Assigned);
        assignment.IsActive.Should().BeTrue();
        f.Assignments.Verify(r => r.AddAsync(It.IsAny<PriceListCustomer>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Asignar_mismo_cliente_a_misma_lista_ya_activa_es_idempotente()
    {
        var f = new Fixture();
        var list = CreateList("A");
        var customer = CreateCustomer();
        var assignment = PriceListCustomer.Create(TenantId, CompanyId, list.Id, CustomerId, UserId);
        f.PriceLists.Setup(r => r.GetByIdAsync(TenantId, list.Id, It.IsAny<CancellationToken>())).ReturnsAsync(list);
        f.BusinessPartners.Setup(r => r.GetByIdAsync(CustomerId, It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        f.Assignments
            .Setup(r => r.GetByCustomerAsync(TenantId, CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { assignment });

        var result = await f.BuildAssignHandler().Handle(
            new AssignCustomerToPriceListCommand(list.Id, CustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Status.Should().Be(PriceListCustomerAssignStatus.AlreadyActive);
        f.Assignments.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Mover_cliente_de_lista_A_a_B_primero_devuelve_Conflict_sin_escribir()
    {
        var f = new Fixture();
        var listA = CreateList("A");
        var listB = CreateList("B");
        var customer = CreateCustomer();
        var assignmentA = PriceListCustomer.Create(TenantId, CompanyId, listA.Id, CustomerId, UserId);
        f.PriceLists.Setup(r => r.GetByIdAsync(TenantId, listB.Id, It.IsAny<CancellationToken>())).ReturnsAsync(listB);
        f.PriceLists.Setup(r => r.GetByIdAsync(TenantId, listA.Id, It.IsAny<CancellationToken>())).ReturnsAsync(listA);
        f.BusinessPartners.Setup(r => r.GetByIdAsync(CustomerId, It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        f.Assignments
            .Setup(r => r.GetByCustomerAsync(TenantId, CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { assignmentA });

        var result = await f.BuildAssignHandler().Handle(
            new AssignCustomerToPriceListCommand(listB.Id, CustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Status.Should().Be(PriceListCustomerAssignStatus.Conflict);
        result.Value!.ConflictingPriceListId.Should().Be(listA.Id);
        result.Value!.ConflictingPriceListName.Should().Be("Lista A");
        assignmentA.IsActive.Should().BeTrue("no debe escribirse nada sin confirmación");
        f.Assignments.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Mover_cliente_de_lista_A_a_B_con_ConfirmSwitch_desactiva_A_y_activa_B()
    {
        var f = new Fixture();
        var listA = CreateList("A");
        var listB = CreateList("B");
        var customer = CreateCustomer();
        var assignmentA = PriceListCustomer.Create(TenantId, CompanyId, listA.Id, CustomerId, UserId);
        f.PriceLists.Setup(r => r.GetByIdAsync(TenantId, listB.Id, It.IsAny<CancellationToken>())).ReturnsAsync(listB);
        f.BusinessPartners.Setup(r => r.GetByIdAsync(CustomerId, It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        f.Assignments
            .Setup(r => r.GetByCustomerAsync(TenantId, CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { assignmentA });

        var result = await f.BuildAssignHandler().Handle(
            new AssignCustomerToPriceListCommand(listB.Id, CustomerId, ConfirmSwitch: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Status.Should().Be(PriceListCustomerAssignStatus.Switched);
        assignmentA.IsActive.Should().BeFalse();
        f.Assignments.Verify(r => r.AddAsync(It.IsAny<PriceListCustomer>(), It.IsAny<CancellationToken>()), Times.Once);
        f.Assignments.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Asignar_cliente_inexistente_devuelve_NotFound()
    {
        var f = new Fixture();
        var list = CreateList("A");
        f.PriceLists.Setup(r => r.GetByIdAsync(TenantId, list.Id, It.IsAny<CancellationToken>())).ReturnsAsync(list);
        f.BusinessPartners.Setup(r => r.GetByIdAsync(CustomerId, It.IsAny<CancellationToken>())).ReturnsAsync((BusinessPartner?)null);

        var result = await f.BuildAssignHandler().Handle(
            new AssignCustomerToPriceListCommand(list.Id, CustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        f.Assignments.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Lista_de_otro_tenant_o_empresa_devuelve_NotFound_sin_escribir()
    {
        // El repositorio real (ForOperationalScope) nunca devuelve una PriceList de otro
        // tenant/empresa — se simula ese fail-closed devolviendo null, exactamente lo que
        // GetByIdAsync real produce para un id fuera de scope.
        var f = new Fixture();
        f.PriceLists
            .Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PriceList?)null);

        var result = await f.BuildAssignHandler().Handle(
            new AssignCustomerToPriceListCommand(Guid.NewGuid(), CustomerId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        f.BusinessPartners.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        f.Assignments.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Listar_clientes_de_lista_de_otro_tenant_devuelve_NotFound()
    {
        var f = new Fixture();
        f.PriceLists
            .Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PriceList?)null);

        var result = await f.BuildListHandler().Handle(new GetPriceListCustomersQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        f.Assignments.Verify(
            r => r.GetByPriceListAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
