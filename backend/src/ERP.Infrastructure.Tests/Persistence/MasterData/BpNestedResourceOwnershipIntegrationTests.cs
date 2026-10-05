using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.UseCases.BpContacts;
using ERP.Application.MasterData.UseCases.BpLocations;
using ERP.Application.MasterData.UseCases.RevokeBusinessPartnerRole;
using ERP.Application.MasterData.UseCases.UpdateRoleConfig;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.MasterData.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Tests.Audit;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence.MasterData;

/// <summary>
/// ZH-BP-NESTED-RESOURCE-OWNERSHIP-01 — ownership de rutas anidadas contra PostgreSQL real, con los
/// repositorios y el query filter de tenant reales:
///   1. Hijo del BP de la ruta → OK.
///   2. Hijo de otro BP del mismo tenant → NotFound y la fila queda intacta en BD.
///   3. Hijo de otro tenant → sigue bloqueado por el query filter (NotFound), con cualquier bpId.
///   4. POST bajo un BP de otro tenant → NotFound, no se inserta ninguna fila.
/// Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class BpNestedResourceOwnershipIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_bp_nested_ownership_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly Guid _actor = Guid.NewGuid();
    private Guid _tenantA;
    private Guid _tenantB;

    private Guid _bpA1;
    private Guid _bpA2;
    private Guid _bpB;

    private Guid _contactA1;
    private Guid _contactA2;
    private Guid _contactB;
    private Guid _locationA1;
    private Guid _locationA2;
    private Guid _locationB;
    private Guid _roleA1;
    private Guid _roleA2;
    private Guid _roleB;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext(Guid.Empty);
        await db.Database.MigrateAsync();

        var tenantA = Tenant.Create("Tenant A", $"a-{Guid.NewGuid():N}"[..16], _actor);
        var tenantB = Tenant.Create("Tenant B", $"b-{Guid.NewGuid():N}"[..16], _actor);
        db.Tenants.AddRange(tenantA, tenantB);
        await db.SaveChangesAsync();
        _tenantA = tenantA.Id;
        _tenantB = tenantB.Id;

        (_bpA1, _contactA1, _locationA1, _roleA1) = Seed(db, _tenantA, "1710034065", "BP A1");
        (_bpA2, _contactA2, _locationA2, _roleA2) = Seed(db, _tenantA, "1710034073", "BP A2");
        (_bpB, _contactB, _locationB, _roleB) = Seed(db, _tenantB, "1710034081", "BP B");
        await db.SaveChangesAsync();
    }

    private (Guid Bp, Guid Contact, Guid Location, Guid Role) Seed(
        ErpDbContext db,
        Guid tenantId,
        string cedula,
        string name
    )
    {
        var bp = BusinessPartner.Create(tenantId, "05", cedula, 1, name, _actor);
        var location = BusinessPartnerLocation.Create(
            tenantId,
            bp.Id,
            "Bodega",
            LocationType.Warehouse,
            LocationPurpose.Delivery,
            "Av. Principal 1",
            _actor
        );
        var contact = BusinessPartnerContact.Create(
            tenantId,
            bp.Id,
            "Ana",
            ContactRole.Purchasing,
            _actor
        );
        var role = BusinessPartnerRole.Create(
            tenantId,
            bp.Id,
            RoleType.Supplier,
            _actor,
            SupplierRoleConfig.Create()
        );
        db.BusinessPartners.Add(bp);
        db.BusinessPartnerLocations.Add(location);
        db.BusinessPartnerContacts.Add(contact);
        db.BusinessPartnerRoles.Add(role);
        return (bp.Id, contact.Id, location.Id, role.Id);
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        return new ErpDbContext(
            options,
            new FixedCurrentTenant(() => tenantId),
            Mock.Of<IPublisher>(),
            new FixedCurrentCompany(() => Guid.Empty)
        );
    }

    private IOperationalContext Ctx(Guid tenantId) =>
        Mock.Of<IOperationalContext>(c => c.TenantId == tenantId && c.UserId == _actor);

    // ── Contacts ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Contacts_ownership_por_BP_y_por_tenant()
    {
        await using var db = CreateContext(_tenantA);
        var repo = new BusinessPartnerContactRepository(db);
        var get = new GetBpContactByIdHandler(repo);

        (await get.Handle(new GetBpContactByIdQuery(_bpA1, _contactA1), default))
            .IsSuccess.Should()
            .BeTrue();
        (await get.Handle(new GetBpContactByIdQuery(_bpA1, _contactA2), default))
            .Code.Should()
            .Be(ApiResponseCodes.Common.NotFound);
        (await get.Handle(new GetBpContactByIdQuery(_bpB, _contactB), default))
            .Code.Should()
            .Be(ApiResponseCodes.Common.NotFound);
        (await get.Handle(new GetBpContactByIdQuery(_bpA1, _contactB), default))
            .Code.Should()
            .Be(ApiResponseCodes.Common.NotFound);

        var update = await new UpdateBpContactHandler(
            repo,
            new BusinessPartnerLocationRepository(db),
            Ctx(_tenantA)
        ).Handle(
            new UpdateBpContactCommand(_bpA1, _contactA2, "Hackeado", ContactRole.Legal),
            default
        );
        var deactivate = await new DeactivateBpContactHandler(repo, Ctx(_tenantA)).Handle(
            new DeactivateBpContactCommand(_bpA1, _contactA2),
            default
        );
        var crossTenant = await new DeactivateBpContactHandler(repo, Ctx(_tenantA)).Handle(
            new DeactivateBpContactCommand(_bpB, _contactB),
            default
        );

        update.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        deactivate.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        crossTenant.Code.Should().Be(ApiResponseCodes.Common.NotFound);

        await using var checkA = CreateContext(_tenantA);
        var a2 = await checkA
            .BusinessPartnerContacts.AsNoTracking()
            .SingleAsync(c => c.Id == _contactA2);
        a2.FirstName.Should().Be("Ana");
        a2.IsActive.Should().BeTrue();
        await using var checkB = CreateContext(_tenantB);
        (await checkB.BusinessPartnerContacts.AsNoTracking().SingleAsync(c => c.Id == _contactB))
            .IsActive.Should()
            .BeTrue();
    }

    [Fact]
    public async Task Contacto_con_ubicacion_de_otro_BP_es_rechazado_y_con_la_propia_se_acepta()
    {
        await using var db = CreateContext(_tenantA);
        var handler = new UpdateBpContactHandler(
            new BusinessPartnerContactRepository(db),
            new BusinessPartnerLocationRepository(db),
            Ctx(_tenantA)
        );

        var foreign = await handler.Handle(
            new UpdateBpContactCommand(
                _bpA1,
                _contactA1,
                "Ana",
                ContactRole.Purchasing,
                LocationId: _locationA2
            ),
            default
        );
        var own = await handler.Handle(
            new UpdateBpContactCommand(
                _bpA1,
                _contactA1,
                "Ana",
                ContactRole.Purchasing,
                LocationId: _locationA1
            ),
            default
        );

        foreign.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        own.IsSuccess.Should().BeTrue(own.Error);
        own.Value!.LocationId.Should().Be(_locationA1);
    }

    [Fact]
    public async Task Crear_contacto_o_ubicacion_bajo_BP_de_otro_tenant_no_inserta_filas()
    {
        await using var db = CreateContext(_tenantA);
        var bpRepo = new BusinessPartnerRepository(db);
        var contactRepo = new BusinessPartnerContactRepository(db);
        var locRepo = new BusinessPartnerLocationRepository(db);

        var contact = await new CreateBpContactHandler(
            bpRepo,
            contactRepo,
            locRepo,
            Ctx(_tenantA)
        ).Handle(
            new CreateBpContactCommand(_bpB, "Intruso", ContactRole.Other, OtherDescription: "x"),
            default
        );
        var location = await new CreateBpLocationHandler(bpRepo, locRepo, Ctx(_tenantA)).Handle(
            new CreateBpLocationCommand(
                _bpB,
                "Intrusa",
                LocationType.Office,
                LocationPurpose.Billing,
                "Calle 9"
            ),
            default
        );

        contact.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        location.Code.Should().Be(ApiResponseCodes.Common.NotFound);

        await using var raw = CreateContext(_tenantA);
        (
            await raw
                .BusinessPartnerContacts.IgnoreQueryFilters()
                .CountAsync(c => c.BusinessPartnerId == _bpB)
        )
            .Should()
            .Be(1);
        (
            await raw
                .BusinessPartnerLocations.IgnoreQueryFilters()
                .CountAsync(l => l.BusinessPartnerId == _bpB)
        )
            .Should()
            .Be(1);
    }

    // ── Locations ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Locations_ownership_por_BP_y_por_tenant()
    {
        await using var db = CreateContext(_tenantA);
        var repo = new BusinessPartnerLocationRepository(db);
        var get = new GetBpLocationByIdHandler(repo);

        (await get.Handle(new GetBpLocationByIdQuery(_bpA1, _locationA1), default))
            .IsSuccess.Should()
            .BeTrue();
        (await get.Handle(new GetBpLocationByIdQuery(_bpA1, _locationA2), default))
            .Code.Should()
            .Be(ApiResponseCodes.Common.NotFound);
        (await get.Handle(new GetBpLocationByIdQuery(_bpB, _locationB), default))
            .Code.Should()
            .Be(ApiResponseCodes.Common.NotFound);

        var update = await new UpdateBpLocationHandler(repo, Ctx(_tenantA)).Handle(
            new UpdateBpLocationCommand(
                _bpA1,
                _locationA2,
                "Hackeada",
                LocationType.Office,
                LocationPurpose.Billing,
                "Otra"
            ),
            default
        );
        var setPrimary = await new SetPrimaryBpLocationHandler(repo, Ctx(_tenantA)).Handle(
            new SetPrimaryBpLocationCommand(_bpA1, _locationA2),
            default
        );
        var crossTenant = await new SetPrimaryBpLocationHandler(repo, Ctx(_tenantA)).Handle(
            new SetPrimaryBpLocationCommand(_bpB, _locationB),
            default
        );

        update.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        setPrimary.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        crossTenant.Code.Should().Be(ApiResponseCodes.Common.NotFound);

        await using var checkA = CreateContext(_tenantA);
        var a2 = await checkA
            .BusinessPartnerLocations.AsNoTracking()
            .SingleAsync(l => l.Id == _locationA2);
        a2.Name.Should().Be("Bodega");
        a2.IsPrimary.Should().BeFalse();
        await using var checkB = CreateContext(_tenantB);
        (await checkB.BusinessPartnerLocations.AsNoTracking().SingleAsync(l => l.Id == _locationB))
            .IsPrimary.Should()
            .BeFalse();
    }

    // ── Roles ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Roles_ownership_por_BP_y_por_tenant()
    {
        await using var db = CreateContext(_tenantA);
        var repo = new BusinessPartnerRoleRepository(db);

        var ownNotes = await new UpdateRoleNotesHandler(repo, Ctx(_tenantA)).Handle(
            new UpdateRoleNotesCommand(_bpA1, _roleA1, "nota propia"),
            default
        );
        var revokeCrossParent = await new RevokeBusinessPartnerRoleHandler(
            repo,
            Ctx(_tenantA)
        ).Handle(new RevokeBusinessPartnerRoleCommand(_bpA1, _roleA2), default);
        var configCrossParent = await new UpdateSupplierRoleConfigHandler(
            repo,
            Ctx(_tenantA)
        ).Handle(
            new UpdateSupplierRoleConfigCommand(
                _bpA1,
                _roleA2,
                SupplierRoleConfigDto.From(SupplierRoleConfig.Create(isRetentionExempt: true))
            ),
            default
        );
        var revokeCrossTenant = await new RevokeBusinessPartnerRoleHandler(
            repo,
            Ctx(_tenantA)
        ).Handle(new RevokeBusinessPartnerRoleCommand(_bpB, _roleB), default);

        ownNotes.IsSuccess.Should().BeTrue(ownNotes.Error);
        revokeCrossParent.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        configCrossParent.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        revokeCrossTenant.Code.Should().Be(ApiResponseCodes.Common.NotFound);

        await using var checkA = CreateContext(_tenantA);
        var a2 = await checkA.BusinessPartnerRoles.AsNoTracking().SingleAsync(r => r.Id == _roleA2);
        a2.IsActive.Should().BeTrue();
        a2.SupplierConfig!.IsRetentionExempt.Should().BeFalse();
        (await checkA.BusinessPartnerRoles.AsNoTracking().SingleAsync(r => r.Id == _roleA1))
            .Notes.Should()
            .Be("nota propia");
        await using var checkB = CreateContext(_tenantB);
        (await checkB.BusinessPartnerRoles.AsNoTracking().SingleAsync(r => r.Id == _roleB))
            .IsActive.Should()
            .BeTrue();
    }
}
