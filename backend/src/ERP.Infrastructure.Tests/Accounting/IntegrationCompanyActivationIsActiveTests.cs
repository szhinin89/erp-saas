using ERP.Application.Common;
using ERP.Application.Common.Security;
using ERP.Application.Modules.Integration;
using ERP.Application.Modules.Integration.UseCases;
using ERP.Domain.Access.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ERP.Infrastructure.Tests.Accounting;

/// <summary>
/// F-02 (API-DOC-V1) sobre PostgreSQL real (fixture desechable): Suspend y Activate solo mueven
/// <c>OperationalStatus</c>; <c>IsActive</c> (habilitación administrativa) se conserva siempre.
/// </summary>
public sealed class IntegrationCompanyActivationIsActiveTests : IClassFixture<AccountingManual08Fixture>
{
    private const string BlockedMessage = "Empresa no disponible para operar.";
    private readonly AccountingManual08Fixture _fixture;
    public IntegrationCompanyActivationIsActiveTests(AccountingManual08Fixture fixture) => _fixture = fixture;
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static async Task<IntegrationCompanyStatusDto> SuspendAsync(ManualBook book)
    {
        await using var db = book.Db();
        return (await new SuspendIntegrationCompanyHandler(new CompanyRepository(db))
            .Handle(new SuspendIntegrationCompanyCommand(book.CompanyId), Ct)).Value!;
    }

    private static async Task<IntegrationCompanyStatusDto> ActivateAsync(ManualBook book)
    {
        await using var db = book.Db();
        return (await new ActivateIntegrationCompanyHandler(new CompanyRepository(db))
            .Handle(new ActivateIntegrationCompanyCommand(book.CompanyId), Ct)).Value!;
    }

    private static async Task SetIsActiveAsync(ManualBook book, bool value)
    {
        await using var db = book.Db();
        var c = await db.Companies.IgnoreQueryFilters().SingleAsync(x => x.Id == book.CompanyId);
        c.IsActive = value;
        await db.SaveChangesAsync();
    }

    private static async Task<Company> ReadAsync(ManualBook book)
    {
        await using var db = book.Db();
        return await db.Companies.AsNoTracking().IgnoreQueryFilters().SingleAsync(c => c.Id == book.CompanyId);
    }

    /// <summary>true si el guard rechaza por estado de la empresa (deshabilitada o no operativa).</summary>
    private static async Task<bool> BlockedByCompanyStateAsync(ManualBook book)
    {
        await using var db = book.Db();
        var tenant = ERP.Domain.Tenants.Entities.Tenant.Create("T", $"t-{Guid.NewGuid():N}"[..12], book.UserId);
        var tenants = new Mock<ERP.Domain.Tenants.Interfaces.ITenantRepository>();
        tenants.Setup(t => t.GetByIdAsync(book.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        var guard = new CompanyAccessGuard(
            Mock.Of<ERP.Domain.Access.Interfaces.IAccessRepository>(),
            new CompanyRepository(db),
            Mock.Of<ICurrentUser>(u => u.IsAuthenticated && u.UserId == book.UserId),
            Mock.Of<ICurrentTenant>(t => t.TenantId == book.TenantId),
            Mock.Of<ICurrentCompany>(c => c.CompanyId == book.CompanyId),
            tenants.Object,
            Mock.Of<ISecurityMetrics>(),
            Mock.Of<IOperatorCompanyAccessPolicy>());
        var access = await guard.RequireMembershipAsync(book.CompanyId, requireActiveCompany: true, cancellationToken: Ct);
        return !access.IsSuccess && access.Error == BlockedMessage;
    }

    private static async Task<string> SnapshotAsync(ManualBook book)
    {
        await using var db = book.Db();
        var c = await db.Companies.AsNoTracking().IgnoreQueryFilters().SingleAsync(x => x.Id == book.CompanyId);
        var memberships = await db.CompanyUserMemberships.AsNoTracking().IgnoreQueryFilters()
            .Where(m => m.CompanyId == book.CompanyId)
            .Select(m => m.IdentityUserId + "|" + m.IsActive + "|" + m.Role + "|" + m.ProfileId)
            .OrderBy(x => x).ToListAsync();
        return $"{c.TenantId}|{c.TaxIdentificationNumber}|{c.LegalName}|{string.Join(";", memberships)}";
    }

    [Fact]
    public async Task Suspend_conserva_IsActive_y_el_guard_bloquea_por_OperationalStatus()
    {
        var book = await _fixture.BookAsync();

        var dto = await SuspendAsync(book);

        dto.OperationalStatus.Should().Be(CompanyOperationalStatus.Suspended);
        dto.IsActive.Should().BeTrue();
        var saved = await ReadAsync(book);
        saved.IsActive.Should().BeTrue();
        saved.OperationalStatus.Should().Be(CompanyOperationalStatus.Suspended);
        (await BlockedByCompanyStateAsync(book)).Should().BeTrue("la suspendida está bloqueada hasta su activación");
    }

    [Fact]
    public async Task Activate_conserva_IsActive_true_cuando_la_empresa_esta_habilitada()
    {
        var book = await _fixture.BookAsync();
        await SuspendAsync(book);

        var dto = await ActivateAsync(book);

        dto.OperationalStatus.Should().Be(CompanyOperationalStatus.Operational);
        dto.IsActive.Should().BeTrue();
        (await ReadAsync(book)).IsActive.Should().BeTrue();
        (await BlockedByCompanyStateAsync(book)).Should().BeFalse("tras activar, la empresa habilitada deja de estar bloqueada");
    }

    [Fact]
    public async Task Activate_conserva_IsActive_false_cuando_la_empresa_esta_deshabilitada()
    {
        var book = await _fixture.BookAsync();
        await SetIsActiveAsync(book, false);
        await SuspendAsync(book);

        var dto = await ActivateAsync(book);

        dto.OperationalStatus.Should().Be(CompanyOperationalStatus.Operational);
        dto.IsActive.Should().BeFalse("Activate no habilita administrativamente una empresa deshabilitada");
        var saved = await ReadAsync(book);
        saved.IsActive.Should().BeFalse();
        saved.OperationalStatus.Should().Be(CompanyOperationalStatus.Operational);
    }

    [Fact]
    public async Task Empresa_deshabilitada_continua_sin_acceso_operativo_antes_y_despues_de_activar()
    {
        var book = await _fixture.BookAsync();
        await SetIsActiveAsync(book, false);
        (await BlockedByCompanyStateAsync(book)).Should().BeTrue();

        await ActivateAsync(book);

        (await BlockedByCompanyStateAsync(book)).Should().BeTrue("OperationalStatus=Operational no reemplaza IsActive=false");
    }

    [Fact]
    public async Task Activate_no_cambia_tenant_ni_membresias()
    {
        var book = await _fixture.BookAsync();
        await using (var seed = book.Db())
        {
            seed.CompanyUserMemberships.Add(
                CompanyUserMembership.Create(book.CompanyId, book.UserId, "Admin", null, book.UserId));
            await seed.SaveChangesAsync();
        }
        await SetIsActiveAsync(book, false);
        await SuspendAsync(book);
        var before = await SnapshotAsync(book);

        await ActivateAsync(book);

        (await SnapshotAsync(book)).Should().Be(before);
        (await ReadAsync(book)).TenantId.Should().Be(book.TenantId);
    }

    [Fact]
    public async Task Transiciones_repetidas_son_idempotentes_y_conservan_IsActive()
    {
        var book = await _fixture.BookAsync();
        await SetIsActiveAsync(book, false);

        for (var i = 0; i < 2; i++)
        {
            var s = await SuspendAsync(book);
            s.OperationalStatus.Should().Be(CompanyOperationalStatus.Suspended);
            s.IsActive.Should().BeFalse();
        }
        for (var i = 0; i < 2; i++)
        {
            var a = await ActivateAsync(book);
            a.OperationalStatus.Should().Be(CompanyOperationalStatus.Operational);
            a.IsActive.Should().BeFalse();
        }
        (await ReadAsync(book)).IsActive.Should().BeFalse();
    }
}
