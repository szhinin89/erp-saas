using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ERP.API.Tests.Support;
using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.ElectronicDocuments;

/// <summary>
/// Caso #32 y R12 Fase B — las acciones del Monitor <c>/reconciliation</c> y
/// <c>/corrected-retransmission</c> exigen su permiso granular en el pipeline HTTP real
/// (<c>RuntimePermissionAuthorizer</c> contra PostgreSQL, Testcontainers). Sin el permiso: 403 y
/// ningún efecto (estado, contadores, auditoría ni intentos). Con el permiso, la misma solicitud
/// supera la autorización y la rechaza el negocio sin llamar al SRI, así que el 403 proviene del
/// permiso y no de otra regla.
/// </summary>
public sealed class ElectronicDocumentActionAuthorizationFixture : IAsyncLifetime
{
    public const string Role = "Cajero";
    private const string SignedKey = "1111111111111111111111111111111111111111111111111";
    private const string RejectedKey = "2222222222222222222222222222222222222222222222222";

    private readonly PostgreSqlTestWebAppFactory _baseFactory = new(useHttpCompanyContext: true);
    private Guid _adminId;

    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid ViewerUserId { get; private set; }
    public Guid OperatorUserId { get; private set; }
    public Guid SignedDocumentId { get; private set; }
    public Guid RejectedDocumentId { get; private set; }

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("JWT__SECRETKEY", IntegrationTestConstants.JwtSecretKey);
        Environment.SetEnvironmentVariable("JWT__ISSUER", "ZHTechnologies");
        Environment.SetEnvironmentVariable("JWT__AUDIENCE", "ERPUsers");

        await _baseFactory.InitializeAsync();
        await _baseFactory.MigrateAsync();
        await SeedAsync();

        _baseFactory.MutableTenant.TenantId = TenantId;
        _baseFactory.MutableUser.Role = Role;
        JobCompanyContext.Current = Guid.Empty;
        JobBranchContext.Current = Guid.Empty;
    }

    public async Task DisposeAsync() => await _baseFactory.DisposeAsync();

    /// <summary>El ICurrentUser del factory es compartido: cada actor lo fija antes de su solicitud.</summary>
    public HttpClient CreateClientFor(Guid userId)
    {
        _baseFactory.MutableUser.UserId = userId;
        var client = _baseFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtFactory.CreateSessionJwt(TenantId, userId, role: Role)
        );
        client.DefaultRequestHeaders.Add("X-Company-Id", CompanyId.ToString());
        client.DefaultRequestHeaders.Add("X-Branch-Id", BranchId.ToString());
        return client;
    }

    public IServiceScope CreateScope() => _baseFactory.Services.CreateScope();

    private async Task SeedAsync()
    {
        using var scope = _baseFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        _adminId = Guid.NewGuid();
        var tenant = Tenant.Create("ZH-EDocActionAuth", $"zh-edaa-{Guid.NewGuid():N}", _adminId);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        TenantId = tenant.Id;

        var company = Company.CreateManaged(
            TenantId,
            taxIdentificationNumber: $"179{Guid.NewGuid():N}"[..13],
            legalName: "Empresa ED S.A.",
            createdBy: _adminId
        );
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        CompanyId = company.Id;

        var branch = Branch.Create(
            TenantId, "Matriz", "Av. Principal 123", "SUC-ED",
            null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, isMainBranch: true, _adminId, companyId: CompanyId
        );
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        BranchId = branch.Id;

        ViewerUserId = await CreateUserAsync(
            db,
            "ED solo consulta",
            ElectronicDocumentsPermissions.View,
            ElectronicDocumentsPermissions.Detail,
            ElectronicDocumentsPermissions.Retry
        );
        OperatorUserId = await CreateUserAsync(
            db,
            "ED conciliación y retransmisión",
            ElectronicDocumentsPermissions.View,
            ElectronicDocumentsPermissions.Detail,
            ElectronicDocumentsPermissions.Reconcile,
            ElectronicDocumentsPermissions.RetransmitCorrected
        );

        var signed = ElectronicDocument.Create(TenantId, CompanyId,
            ElectronicDocumentType.Invoice, "Sales", Guid.NewGuid(), _adminId);
        signed.MarkXmlGenerated("draft.xml", "1.1.0", "1.1.0", _adminId);
        signed.MarkSigned("signed.xml", AccessKey.Create(SignedKey), _adminId);
        var rejected = ElectronicDocument.Create(TenantId, CompanyId,
            ElectronicDocumentType.CreditNote, "Sales", Guid.NewGuid(), _adminId);
        rejected.MarkXmlGenerated("draft.xml", "1.1.0", "1.1.0", _adminId);
        rejected.MarkSigned("signed.xml", AccessKey.Create(RejectedKey), _adminId);
        rejected.MarkSent(_adminId);
        rejected.MarkReceived(_adminId);
        rejected.MarkRejected("[52] ERROR EN DIFERENCIAS", _adminId);
        // Sembrado fuera de una solicitud: sin empresa en contexto el handler de auditoría no aplica.
        signed.ClearDomainEvents();
        rejected.ClearDomainEvents();
        db.ElectronicDocuments.AddRange(signed, rejected);
        await db.SaveChangesAsync();
        SignedDocumentId = signed.Id;
        RejectedDocumentId = rejected.Id;
    }

    private async Task<Guid> CreateUserAsync(ErpDbContext db, string profileName, params string[] permissions)
    {
        var profile = AccessProfile.Create(TenantId, CompanyId, profileName, null, _adminId);
        db.AccessProfiles.Add(profile);
        await db.SaveChangesAsync();
        foreach (var key in permissions)
            db.AccessProfilePermissions.Add(
                AccessProfilePermission.Create(TenantId, profile.Id, key, true, _adminId)
            );
        var user = IdentityUser.Create(
            $"usuario-{Guid.NewGuid():N}",
            "Usuario",
            "Prueba",
            $"usuario-{Guid.NewGuid():N}@example.com",
            "hash",
            _adminId
        );
        db.IdentityUsers.Add(user);
        await db.SaveChangesAsync();
        var membership = CompanyUserMembership.Create(CompanyId, user.Id, Role, profile.Id, _adminId);
        db.CompanyUserMemberships.Add(membership);
        await db.SaveChangesAsync();
        db.CompanyUserBranches.Add(
            CompanyUserBranch.Create(TenantId, CompanyId, membership.Id, BranchId, _adminId)
        );
        await db.SaveChangesAsync();
        return user.Id;
    }
}

[Trait("Category", "PostgreSql")]
public sealed class ElectronicDocumentActionAuthorizationHttpTests
    : IClassFixture<ElectronicDocumentActionAuthorizationFixture>
{
    private readonly ElectronicDocumentActionAuthorizationFixture _f;

    public ElectronicDocumentActionAuthorizationHttpTests(ElectronicDocumentActionAuthorizationFixture fixture) =>
        _f = fixture;

    private sealed record Snapshot(
        ElectronicDocumentState State,
        int RetryCount,
        int TransmissionAttempt,
        string? SignedXmlPath,
        int Audits,
        int Attempts
    );

    private async Task<Snapshot> SnapshotAsync(Guid documentId)
    {
        using var scope = _f.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var document = await db.ElectronicDocuments.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(d => d.Id == documentId);
        return new(
            document.CurrentState,
            document.RetryCount,
            document.TransmissionAttempt,
            document.SignedXmlPath,
            await db.Set<ElectronicDocumentAudit>().IgnoreQueryFilters().CountAsync(a => a.EntityId == documentId),
            await db.ElectronicDocumentTransmissionAttempts.IgnoreQueryFilters()
                .CountAsync(a => a.ElectronicDocumentId == documentId)
        );
    }

    private static object ReconciliationBody() =>
        new { reference = "SRI en Línea caso 32", observation = "Sin resultado definitivo" };

    private static object RetransmissionBody() =>
        new { reason = "Corrección de dirección matriz", idempotencyKey = Guid.NewGuid() };

    [Fact]
    public async Task Conciliar_sin_permiso_reconcile_devuelve_403_sin_efectos()
    {
        var before = await SnapshotAsync(_f.SignedDocumentId);
        using var viewer = _f.CreateClientFor(_f.ViewerUserId);

        using var response = await viewer.PostAsJsonAsync(
            $"/api/v1/electronic-documents/{_f.SignedDocumentId}/reconciliation",
            ReconciliationBody()
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
        (await SnapshotAsync(_f.SignedDocumentId)).Should().Be(before);
        before.State.Should().Be(ElectronicDocumentState.Signed);
    }

    [Fact]
    public async Task Retransmision_corregida_sin_permiso_devuelve_403_sin_efectos()
    {
        var before = await SnapshotAsync(_f.RejectedDocumentId);
        using var viewer = _f.CreateClientFor(_f.ViewerUserId);

        using var response = await viewer.PostAsJsonAsync(
            $"/api/v1/electronic-documents/{_f.RejectedDocumentId}/corrected-retransmission",
            RetransmissionBody()
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
        (await SnapshotAsync(_f.RejectedDocumentId)).Should().Be(before);
        before.State.Should().Be(ElectronicDocumentState.Rejected);
        before.Attempts.Should().Be(0);
    }

    [Fact]
    public async Task Con_el_permiso_la_solicitud_supera_la_autorizacion_y_la_rechaza_el_negocio_sin_efectos()
    {
        // Conciliar un comprobante con resultado conocido (Rejected) y retransmitir uno que no está
        // rechazado (Signed) son rechazos de negocio previos a toda llamada al SRI.
        var rejectedBefore = await SnapshotAsync(_f.RejectedDocumentId);
        var signedBefore = await SnapshotAsync(_f.SignedDocumentId);
        using var operatorClient = _f.CreateClientFor(_f.OperatorUserId);

        using var reconcile = await operatorClient.PostAsJsonAsync(
            $"/api/v1/electronic-documents/{_f.RejectedDocumentId}/reconciliation",
            ReconciliationBody()
        );
        using var retransmit = await operatorClient.PostAsJsonAsync(
            $"/api/v1/electronic-documents/{_f.SignedDocumentId}/corrected-retransmission",
            RetransmissionBody()
        );

        reconcile.StatusCode.Should().NotBe(HttpStatusCode.Forbidden).And.NotBe(HttpStatusCode.Unauthorized);
        reconcile.IsSuccessStatusCode.Should().BeFalse(await reconcile.Content.ReadAsStringAsync());
        retransmit.StatusCode.Should().NotBe(HttpStatusCode.Forbidden).And.NotBe(HttpStatusCode.Unauthorized);
        retransmit.IsSuccessStatusCode.Should().BeFalse(await retransmit.Content.ReadAsStringAsync());
        (await SnapshotAsync(_f.RejectedDocumentId)).Should().Be(rejectedBefore);
        (await SnapshotAsync(_f.SignedDocumentId)).Should().Be(signedBefore);
    }
}
