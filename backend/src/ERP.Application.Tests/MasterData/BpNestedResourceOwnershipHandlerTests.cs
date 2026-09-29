using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.UseCases.BpContacts;
using ERP.Application.MasterData.UseCases.BpLocations;
using ERP.Application.MasterData.UseCases.RevokeBusinessPartnerRole;
using ERP.Application.MasterData.UseCases.UpdateRoleConfig;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.MasterData;

/// <summary>
/// ZH-BP-NESTED-RESOURCE-OWNERSHIP-01 — las rutas anidadas
/// <c>/business-partners/{bpId}/(roles|contacts|locations)/{childId}</c> validan en Application que
/// el hijo pertenezca al BP de la ruta. Un hijo de otro BP responde exactamente igual que un id
/// inexistente (NotFound, mismo mensaje) y nunca se modifica ni se persiste nada.
/// El aislamiento cross-tenant del query filter real se cubre en
/// ERP.Infrastructure.Tests/Persistence/MasterData/BpNestedResourceOwnershipIntegrationTests.
/// </summary>
public sealed class BpNestedResourceOwnershipHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BpA = Guid.NewGuid();
    private static readonly Guid BpB = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly Mock<IOperationalContext> _ctx = new();

    public BpNestedResourceOwnershipHandlerTests()
    {
        _ctx.Setup(c => c.TenantId).Returns(TenantId);
        _ctx.Setup(c => c.UserId).Returns(UserId);
    }

    private static (bool Ok, string? Code, string? Error) Outcome<T>(Result<T> r) =>
        (r.IsSuccess, r.Code, r.Error);

    // ── Contacts ─────────────────────────────────────────────────────────────

    private sealed class ContactFixture
    {
        public Mock<IBusinessPartnerRepository> BpRepo { get; } = new();
        public Mock<IBusinessPartnerContactRepository> Repo { get; } = new();
        public Mock<IBusinessPartnerLocationRepository> LocRepo { get; } = new();
        public BusinessPartnerContact Contact { get; }

        public ContactFixture(bool active)
        {
            Contact = BusinessPartnerContact.Create(TenantId, BpA, "Ana", ContactRole.Purchasing, UserId);
            if (!active)
                Contact.Deactivate(UserId);
            Repo.Setup(r => r.GetByIdAsync(Contact.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Contact);
        }
    }

    public static TheoryData<string> ContactOperations => new() { "get", "update", "set-primary", "deactivate", "activate" };

    private async Task<(bool Ok, string? Code, string? Error)> RunContact(
        string op,
        ContactFixture f,
        Guid bpId,
        Guid contactId
    ) =>
        op switch
        {
            "get" => Outcome(await new GetBpContactByIdHandler(f.Repo.Object).Handle(new GetBpContactByIdQuery(bpId, contactId), default)),
            "update" => Outcome(await new UpdateBpContactHandler(f.Repo.Object, f.LocRepo.Object, _ctx.Object)
                .Handle(new UpdateBpContactCommand(bpId, contactId, "Nuevo", ContactRole.Commercial), default)),
            "set-primary" => Outcome(await new SetPrimaryBpContactHandler(f.Repo.Object, _ctx.Object)
                .Handle(new SetPrimaryBpContactCommand(bpId, contactId), default)),
            "deactivate" => Outcome(await new DeactivateBpContactHandler(f.Repo.Object, _ctx.Object)
                .Handle(new DeactivateBpContactCommand(bpId, contactId), default)),
            "activate" => Outcome(await new ActivateBpContactHandler(f.Repo.Object, _ctx.Object)
                .Handle(new ActivateBpContactCommand(bpId, contactId), default)),
            _ => throw new ArgumentOutOfRangeException(nameof(op)),
        };

    [Theory]
    [MemberData(nameof(ContactOperations))]
    public async Task Contacto_del_BP_de_la_ruta_se_procesa(string op)
    {
        var f = new ContactFixture(active: op != "activate");

        var result = await RunContact(op, f, BpA, f.Contact.Id);

        result.Ok.Should().BeTrue(result.Error);
    }

    [Theory]
    [MemberData(nameof(ContactOperations))]
    public async Task Contacto_de_otro_BP_responde_NotFound_igual_que_inexistente_y_no_se_modifica(string op)
    {
        var f = new ContactFixture(active: op != "activate");
        var before = (f.Contact.FirstName, f.Contact.IsActive, f.Contact.IsPrimary, f.Contact.Role);

        var crossParent = await RunContact(op, f, BpB, f.Contact.Id);
        var missing = await RunContact(op, f, BpB, Guid.NewGuid());

        crossParent.Ok.Should().BeFalse();
        crossParent.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        crossParent.Should().Be(missing, "un hijo de otro BP no debe distinguirse de uno inexistente");
        (f.Contact.FirstName, f.Contact.IsActive, f.Contact.IsPrimary, f.Contact.Role).Should().Be(before);
        f.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        f.Repo.Verify(r => r.ClearPrimaryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Crear_contacto_bajo_BP_fuera_del_scope_responde_NotFound_sin_persistir()
    {
        var f = new ContactFixture(active: true);
        f.BpRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BusinessPartner?)null);

        var result = await new CreateBpContactHandler(f.BpRepo.Object, f.Repo.Object, f.LocRepo.Object, _ctx.Object)
            .Handle(new CreateBpContactCommand(BpB, "Ana", ContactRole.Purchasing, IsPrimary: true), default);

        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        f.Repo.Verify(r => r.AddAsync(It.IsAny<BusinessPartnerContact>(), It.IsAny<CancellationToken>()), Times.Never);
        f.Repo.Verify(r => r.ClearPrimaryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Contacto_no_puede_referenciar_ubicacion_de_otro_BP_ni_inexistente()
    {
        var f = new ContactFixture(active: true);
        var bpA = BusinessPartner.Create(TenantId, "04", "1791352688001", 2, "BP A", UserId);
        f.BpRepo.Setup(r => r.GetByIdAsync(BpA, It.IsAny<CancellationToken>())).ReturnsAsync(bpA);
        var locationOfB = NewLocation(BpB);
        f.LocRepo.Setup(r => r.GetByIdAsync(locationOfB.Id, It.IsAny<CancellationToken>())).ReturnsAsync(locationOfB);

        var create = await new CreateBpContactHandler(f.BpRepo.Object, f.Repo.Object, f.LocRepo.Object, _ctx.Object)
            .Handle(new CreateBpContactCommand(BpA, "Ana", ContactRole.Purchasing, LocationId: locationOfB.Id), default);
        var update = await new UpdateBpContactHandler(f.Repo.Object, f.LocRepo.Object, _ctx.Object)
            .Handle(new UpdateBpContactCommand(BpA, f.Contact.Id, "Ana", ContactRole.Purchasing, LocationId: locationOfB.Id), default);
        var updateMissing = await new UpdateBpContactHandler(f.Repo.Object, f.LocRepo.Object, _ctx.Object)
            .Handle(new UpdateBpContactCommand(BpA, f.Contact.Id, "Ana", ContactRole.Purchasing, LocationId: Guid.NewGuid()), default);

        create.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        update.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        (update.Error, update.Code).Should().Be((updateMissing.Error, updateMissing.Code));
        f.Contact.LocationId.Should().BeNull();
        f.Repo.Verify(r => r.AddAsync(It.IsAny<BusinessPartnerContact>(), It.IsAny<CancellationToken>()), Times.Never);
        f.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Contacto_puede_referenciar_ubicacion_del_mismo_BP()
    {
        var f = new ContactFixture(active: true);
        var locationOfA = NewLocation(BpA);
        f.LocRepo.Setup(r => r.GetByIdAsync(locationOfA.Id, It.IsAny<CancellationToken>())).ReturnsAsync(locationOfA);

        var result = await new UpdateBpContactHandler(f.Repo.Object, f.LocRepo.Object, _ctx.Object)
            .Handle(new UpdateBpContactCommand(BpA, f.Contact.Id, "Ana", ContactRole.Purchasing, LocationId: locationOfA.Id), default);

        result.IsSuccess.Should().BeTrue(result.Error);
        f.Contact.LocationId.Should().Be(locationOfA.Id);
    }

    // ── Locations ────────────────────────────────────────────────────────────

    private static BusinessPartnerLocation NewLocation(Guid bpId) =>
        BusinessPartnerLocation.Create(TenantId, bpId, "Bodega", LocationType.Warehouse, LocationPurpose.Delivery, "Av. Siempre Viva 123", UserId);

    private sealed class LocationFixture
    {
        public Mock<IBusinessPartnerRepository> BpRepo { get; } = new();
        public Mock<IBusinessPartnerLocationRepository> Repo { get; } = new();
        public BusinessPartnerLocation Location { get; }

        public LocationFixture(bool active)
        {
            Location = NewLocation(BpA);
            if (!active)
                Location.Deactivate(UserId);
            Repo.Setup(r => r.GetByIdAsync(Location.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Location);
        }
    }

    public static TheoryData<string> LocationOperations => new() { "get", "update", "set-primary", "deactivate", "activate" };

    private async Task<(bool Ok, string? Code, string? Error)> RunLocation(
        string op,
        LocationFixture f,
        Guid bpId,
        Guid locationId
    ) =>
        op switch
        {
            "get" => Outcome(await new GetBpLocationByIdHandler(f.Repo.Object).Handle(new GetBpLocationByIdQuery(bpId, locationId), default)),
            "update" => Outcome(await new UpdateBpLocationHandler(f.Repo.Object, _ctx.Object)
                .Handle(new UpdateBpLocationCommand(bpId, locationId, "Matriz", LocationType.Matrix, LocationPurpose.Billing, "Calle 1"), default)),
            "set-primary" => Outcome(await new SetPrimaryBpLocationHandler(f.Repo.Object, _ctx.Object)
                .Handle(new SetPrimaryBpLocationCommand(bpId, locationId), default)),
            "deactivate" => Outcome(await new DeactivateBpLocationHandler(f.Repo.Object, _ctx.Object)
                .Handle(new DeactivateBpLocationCommand(bpId, locationId), default)),
            "activate" => Outcome(await new ActivateBpLocationHandler(f.Repo.Object, _ctx.Object)
                .Handle(new ActivateBpLocationCommand(bpId, locationId), default)),
            _ => throw new ArgumentOutOfRangeException(nameof(op)),
        };

    [Theory]
    [MemberData(nameof(LocationOperations))]
    public async Task Ubicacion_del_BP_de_la_ruta_se_procesa(string op)
    {
        var f = new LocationFixture(active: op != "activate");

        var result = await RunLocation(op, f, BpA, f.Location.Id);

        result.Ok.Should().BeTrue(result.Error);
    }

    [Theory]
    [MemberData(nameof(LocationOperations))]
    public async Task Ubicacion_de_otro_BP_responde_NotFound_igual_que_inexistente_y_no_se_modifica(string op)
    {
        var f = new LocationFixture(active: op != "activate");
        var before = (f.Location.Name, f.Location.IsActive, f.Location.IsPrimary, f.Location.Type);

        var crossParent = await RunLocation(op, f, BpB, f.Location.Id);
        var missing = await RunLocation(op, f, BpB, Guid.NewGuid());

        crossParent.Ok.Should().BeFalse();
        crossParent.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        crossParent.Should().Be(missing, "un hijo de otro BP no debe distinguirse de uno inexistente");
        (f.Location.Name, f.Location.IsActive, f.Location.IsPrimary, f.Location.Type).Should().Be(before);
        f.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        f.Repo.Verify(r => r.ClearPrimaryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        f.Repo.Verify(r => r.HasActiveContactsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Crear_ubicacion_bajo_BP_fuera_del_scope_responde_NotFound_sin_persistir()
    {
        var f = new LocationFixture(active: true);
        f.BpRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BusinessPartner?)null);

        var result = await new CreateBpLocationHandler(f.BpRepo.Object, f.Repo.Object, _ctx.Object)
            .Handle(new CreateBpLocationCommand(BpB, "Bodega", LocationType.Warehouse, LocationPurpose.Delivery, "Calle 1", IsPrimary: true), default);

        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        f.Repo.Verify(r => r.AddAsync(It.IsAny<BusinessPartnerLocation>(), It.IsAny<CancellationToken>()), Times.Never);
        f.Repo.Verify(r => r.ClearPrimaryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Roles ────────────────────────────────────────────────────────────────

    private sealed class RoleFixture
    {
        public Mock<IBusinessPartnerRoleRepository> Repo { get; } = new();
        public BusinessPartnerRole Role { get; }

        public RoleFixture(RoleType type)
        {
            Role = type switch
            {
                RoleType.Supplier => BusinessPartnerRole.Create(TenantId, BpA, type, UserId, SupplierRoleConfig.Create()),
                RoleType.Carrier => BusinessPartnerRole.Create(TenantId, BpA, type, UserId, carrierConfig: CarrierRoleConfig.Create()),
                _ => BusinessPartnerRole.Create(TenantId, BpA, type, UserId, customerConfig: CustomerRoleConfig.Create()),
            };
            Repo.Setup(r => r.GetByIdAsync(Role.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Role);
        }
    }

    public static TheoryData<string> RoleOperations => new() { "revoke", "supplier-config", "carrier-config", "customer-config", "notes" };

    private static RoleType RoleTypeFor(string op) =>
        op switch
        {
            "carrier-config" => RoleType.Carrier,
            "customer-config" => RoleType.Customer,
            _ => RoleType.Supplier,
        };

    private async Task<(bool Ok, string? Code, string? Error)> RunRole(string op, RoleFixture f, Guid bpId, Guid roleId) =>
        op switch
        {
            "revoke" => Outcome(await new RevokeBusinessPartnerRoleHandler(f.Repo.Object, _ctx.Object)
                .Handle(new RevokeBusinessPartnerRoleCommand(bpId, roleId), default)),
            "supplier-config" => Outcome(await new UpdateSupplierRoleConfigHandler(f.Repo.Object, _ctx.Object)
                .Handle(new UpdateSupplierRoleConfigCommand(bpId, roleId, SupplierRoleConfigDto.From(SupplierRoleConfig.Create(isRetentionExempt: true))), default)),
            "carrier-config" => Outcome(await new UpdateCarrierRoleConfigHandler(f.Repo.Object, _ctx.Object)
                .Handle(new UpdateCarrierRoleConfigCommand(bpId, roleId, CarrierRoleConfigDto.From(CarrierRoleConfig.Create("AUT-1", 10m))), default)),
            "customer-config" => Outcome(await new UpdateCustomerRoleConfigHandler(f.Repo.Object, _ctx.Object)
                .Handle(new UpdateCustomerRoleConfigCommand(bpId, roleId, CustomerRoleConfigDto.From(CustomerRoleConfig.Create(salesZone: "Norte"))), default)),
            "notes" => Outcome(await new UpdateRoleNotesHandler(f.Repo.Object, _ctx.Object)
                .Handle(new UpdateRoleNotesCommand(bpId, roleId, "nota"), default)),
            _ => throw new ArgumentOutOfRangeException(nameof(op)),
        };

    [Theory]
    [MemberData(nameof(RoleOperations))]
    public async Task Rol_del_BP_de_la_ruta_se_procesa(string op)
    {
        var f = new RoleFixture(RoleTypeFor(op));

        var result = await RunRole(op, f, BpA, f.Role.Id);

        result.Ok.Should().BeTrue(result.Error);
    }

    [Theory]
    [MemberData(nameof(RoleOperations))]
    public async Task Rol_de_otro_BP_responde_NotFound_igual_que_inexistente_y_no_se_modifica(string op)
    {
        var f = new RoleFixture(RoleTypeFor(op));
        var before = (f.Role.IsActive, f.Role.Notes, f.Role.SupplierConfig, f.Role.CarrierConfig, f.Role.CustomerConfig);

        var crossParent = await RunRole(op, f, BpB, f.Role.Id);
        var missing = await RunRole(op, f, BpB, Guid.NewGuid());

        crossParent.Ok.Should().BeFalse();
        crossParent.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        crossParent.Should().Be(missing, "un hijo de otro BP no debe distinguirse de uno inexistente");
        (f.Role.IsActive, f.Role.Notes, f.Role.SupplierConfig, f.Role.CarrierConfig, f.Role.CustomerConfig).Should().Be(before);
        f.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
