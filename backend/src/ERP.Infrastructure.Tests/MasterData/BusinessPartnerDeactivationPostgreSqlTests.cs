using ERP.Application.Common;
using ERP.Application.MasterData.UseCases.DeactivateBusinessPartner;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.ValueObjects;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.InitialLoad;
using ERP.Infrastructure.MasterData.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Tests.InitialLoad;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using BranchEntity = ERP.Domain.Branches.Entities.Branch;

namespace ERP.Infrastructure.Tests.MasterData;

/// <summary>
/// API-DOC-F-51 — caracterización en PostgreSQL real del comportamiento VIGENTE de
/// <c>DeactivateBusinessPartnerHandler</c>: desactivar un tercero con roles activos, un documento en
/// curso y una obligación de cobro pendiente tiene éxito y no toca roles ni documentos. Ninguna SSOT
/// define hoy una regla de bloqueo para la desactivación (ADR-BP-14 solo trata la revocación de
/// roles, diferida); esta prueba documenta el estado actual, no fija una política. Si el propietario
/// decide bloquear, esta prueba debe cambiar junto con esa decisión.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class BusinessPartnerDeactivationPostgreSqlTests : IClassFixture<InitialLoadPostgresFixture>
{
    private static long _tax = 1790055000;
    private readonly InitialLoadPostgresFixture _pg;
    private readonly Guid _user = Guid.NewGuid();

    public BusinessPartnerDeactivationPostgreSqlTests(InitialLoadPostgresFixture pg) => _pg = pg;

    private ErpDbContext NewDb(Guid tenant, Guid company)
    {
        var t = new Mock<ICurrentTenant>();
        t.SetupGet(x => x.TenantId).Returns(tenant);
        var c = new Mock<ICurrentCompany>();
        c.SetupGet(x => x.CompanyId).Returns(company);
        c.SetupGet(x => x.HasCompanyContext).Returns(true);
        return new ErpDbContext(
            new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_pg.ConnectionString).Options,
            t.Object, Mock.Of<IPublisher>(), c.Object);
    }

    [Fact]
    public async Task Desactivar_tercero_con_roles_documento_en_curso_y_cobro_pendiente_tiene_exito_sin_tocar_nada()
    {
        var tenant = Tenant.Create("F51", "f51-" + Guid.NewGuid().ToString("N")[..8], _user);
        var company = Company.CreateManaged(tenant.Id, Interlocked.Increment(ref _tax) + "001", "F51 S.A.", createdBy: _user);
        await using var db = NewDb(tenant.Id, company.Id);
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        var branch = BranchEntity.Create(tenantId: tenant.Id, name: "Matriz", address: "Av. 1", code: "B01",
            description: null, reference: null, postalCode: null, phone: null, secondaryPhone: null, email: null,
            website: null, managerName: null, managerPosition: null, managerEmail: null, managerPhone: null,
            countryId: null, provinceId: null, cantonId: null, parishId: null, latitude: null, longitude: null,
            openingDate: null, internalNotes: null, isMainBranch: true, createdBy: _user, companyId: company.Id);
        db.Branches.Add(branch);
        var bp = BusinessPartner.Create(tenant.Id, "04", "1790016919001", null, "Tercero F51 S.A.", _user);
        db.BusinessPartners.Add(bp);
        await db.SaveChangesAsync();
        db.BusinessPartnerRoles.Add(BusinessPartnerRole.Create(tenant.Id, bp.Id, RoleType.Customer, _user));
        db.BusinessPartnerRoles.Add(BusinessPartnerRole.Create(tenant.Id, bp.Id, RoleType.Supplier, _user));

        var register = ERP.Domain.Modules.Caja.Entities.CashRegister.Create(
            tenant.Id, company.Id, branch.Id, "CAJA-01", "Caja Principal", _user);
        var establishment = Establishment.Create(tenant.Id, branch.Id, company.Id, "001", "F51",
            "Dir", null, true, _user);
        db.Establishments.Add(establishment);
        db.CashRegisters.Add(register);
        await db.SaveChangesAsync();
        var ep = EmissionPoint.Create(tenant.Id, company.Id, establishment.Id, "001", "F51",
            EmissionType.Physical, true, _user);
        db.EmissionPoints.Add(ep);
        await db.SaveChangesAsync();
        var session = ERP.Domain.Modules.Caja.Entities.CashSession.Open(
            tenant.Id, company.Id, branch.Id, _user, register.Id, "CAJA-01", "Caja Principal",
            ep.Id, "001", 0m, _user);
        db.CashSessions.Add(session);
        await db.SaveChangesAsync();

        // Documento en curso: factura de venta en borrador.
        var draft = SalesInvoice.CreateDraft(tenant.Id, company.Id, branch.Id, bp.Id,
            CustomerSnapshot.Create("Tercero F51", "1790016919001", "04"), "DRAFT-F51",
            new DateOnly(2026, 7, 25), _user,
            PaymentTermSnapshot.Create(Guid.NewGuid(), "Contado", installments: 1, daysBetween: 0),
            session.Id, EmissionType.Physical);
        db.SalesInvoices.Add(draft);
        // Obligación pendiente de cobro.
        db.SalesReceivables.Add(SalesReceivable.Create(tenant.Id, company.Id, draft.Id, bp.Id, 100m, _user));
        await db.SaveChangesAsync();

        var ctx = new Mock<IOperationalContext>();
        ctx.SetupGet(x => x.UserId).Returns(_user);
        var handler = new DeactivateBusinessPartnerHandler(new BusinessPartnerRepository(db), ctx.Object);

        var result = await handler.Handle(new DeactivateBusinessPartnerCommand(bp.Id), CancellationToken.None);

        await using var verify = NewDb(tenant.Id, company.Id);
        var persisted = await verify.BusinessPartners.AsNoTracking().FirstAsync(x => x.Id == bp.Id);
        var activeRoles = await verify.BusinessPartnerRoles.AsNoTracking()
            .CountAsync(x => x.BusinessPartnerId == bp.Id && x.IsActive);
        var receivables = await verify.SalesReceivables.AsNoTracking().CountAsync(x => x.CustomerId == bp.Id);

        result.IsSuccess.Should().BeTrue();
        persisted.IsActive.Should().BeFalse();
        activeRoles.Should().Be(2, "la desactivación no revoca roles");
        receivables.Should().Be(1, "la desactivación no altera obligaciones ni documentos");
        (await verify.SalesInvoices.AsNoTracking().CountAsync(x => x.CustomerId == bp.Id)).Should().Be(1);
    }
}
