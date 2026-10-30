using System.Security.Claims;
using ERP.Application;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.ElectronicDocuments.UseCases.ReconcileElectronicDocument;
using ERP.Domain.Access.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.ElectronicDocuments;

// Starts at the persisted Signed boundary: XML generation/signing are outside this case.
[Trait("Category", "PostgreSql")]
public sealed class ElectronicDocumentUncertainSriPostgreSqlTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_uncertain_sri_test")
        .WithUsername("erp").WithPassword("erp_test_secret").Build();
    private Guid _tenant;
    private Guid _company;
    private Guid _user;
    private const string Key = "1111111111111111111111111111111111111111111111111";

    public Task InitializeAsync() => _postgres.StartAsync();
    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task Signed_unknown_outcome_is_only_queried_and_reconciliation_is_audited_with_scope_isolation()
    {
        var seedActor = Guid.NewGuid();
        var seedTenant = Tenant.Create("Caso 32", "case32", seedActor);
        var seedCompany = Company.CreateManaged(seedTenant.Id, "1790012345001", "Caso 32", createdBy: seedActor);
        var user = IdentityUser.Create("case32", "Caso", "32", null, "TEST_ONLY_HASH", seedActor);
        _tenant = seedTenant.Id;
        _company = seedCompany.Id;
        _user = user.Id;
        var authorization = new Mock<IElectronicDocumentAuthorizationService>(MockBehavior.Strict);
        authorization.Setup(x => x.CheckAsync(_company, Key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SriAuthorizationResult>.Failure("SRI sin respuesta"));
        var reception = new Mock<IElectronicDocumentReceptionService>(MockBehavior.Strict);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDistributedMemoryCache();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddApplication();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
            }).Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure(configuration);
        // Same PostgreSQL harness as the existing repository tests; real publisher/audit handlers.
        services.AddScoped(sp => new ErpDbContext(
            new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options,
            sp.GetRequiredService<ICurrentTenant>(), sp.GetRequiredService<IPublisher>(),
            sp.GetRequiredService<ICurrentCompany>()));
        services.AddSingleton(authorization.Object);
        services.AddSingleton(reception.Object);
        await using var provider = services.BuildServiceProvider();
        var accessor = provider.GetRequiredService<IHttpContextAccessor>();
        void SetContext(Guid tenant, Guid company)
        {
            accessor.HttpContext = new DefaultHttpContext();
            accessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("tenant_id", tenant.ToString()),
             new Claim(ClaimTypes.NameIdentifier, _user.ToString()),
             new Claim(ClaimTypes.Name, "Caso 32")], "Test"));
            accessor.HttpContext.Request.Headers["X-Company-Id"] = company.ToString();
        }
        SetContext(_tenant, _company);
        Guid id;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            await db.Database.MigrateAsync();
            db.Tenants.Add(seedTenant);
            db.Companies.Add(seedCompany);
            db.IdentityUsers.Add(user);
            db.CompanyUserMemberships.Add(CompanyUserMembership.Create(_company, _user, "User", null, seedActor));
            await db.SaveChangesAsync();
            var document = ElectronicDocument.Create(_tenant, _company,
                ElectronicDocumentType.Invoice, "Sales", Guid.NewGuid(), _user);
            document.MarkXmlGenerated("draft.xml", "1.1.0", "1.1.0", _user);
            document.MarkSigned("signed.xml", AccessKey.Create(Key), _user);
            id = document.Id;
            var repository = scope.ServiceProvider.GetRequiredService<IElectronicDocumentRepository>();
            await repository.AddAsync(document);
            await repository.SaveChangesAsync();
        }
        await using (var scope = provider.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<IElectronicDocumentIssuer>()
                .RetryAsync(_tenant, id, _user);
            result.IsSuccess.Should().BeTrue(result.Error);
        }
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var document = await db.ElectronicDocuments.SingleAsync(x => x.Id == id);
            document.CurrentState.Should().Be(ElectronicDocumentState.Signed);
            document.RequiresSriReconciliation.Should().BeTrue();
            document.AccessKey!.Value.Should().Be(Key);
            document.RetryCount.Should().Be(1);
            var handler = ActivatorUtilities.CreateInstance<ReconcileElectronicDocumentCommandHandler>(scope.ServiceProvider);
            var result = await handler.Handle(new(id, "SRI en Línea caso 32", "Sin resultado definitivo"), default);
            result.IsSuccess.Should().BeTrue(result.Error);
        }
        foreach (var (tenant, company) in new[]
                 { (_tenant, Guid.NewGuid()), (Guid.NewGuid(), _company) })
        {
            SetContext(tenant, company);
            await using var scope = provider.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IElectronicDocumentRepository>();
            (await repository.GetByIdAsync(tenant, id)).Should().BeNull();
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            (await db.Set<ElectronicDocumentAudit>().AnyAsync(x => x.EntityId == id)).Should().BeFalse();
            var handler = ActivatorUtilities.CreateInstance<ReconcileElectronicDocumentCommandHandler>(scope.ServiceProvider);
            (await handler.Handle(new(id, "foreign", "must not persist"), default)).IsSuccess.Should().BeFalse();
            (await scope.ServiceProvider.GetRequiredService<IElectronicDocumentIssuer>()
                .RetryAsync(tenant, id, _user)).IsSuccess.Should().BeFalse();
        }
        SetContext(_tenant, _company);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var document = await db.ElectronicDocuments.SingleAsync(x => x.Id == id);
            document.CurrentState.Should().Be(ElectronicDocumentState.Signed);
            document.RequiresSriReconciliation.Should().BeTrue();
            document.AccessKey!.Value.Should().Be(Key);
            document.AuthorizationNumber.Should().BeNull();
            document.RetryCount.Should().Be(2);
            var audits = await db.Set<ElectronicDocumentAudit>().Where(x => x.EntityId == id).ToListAsync();
            var evidence = audits.Should().ContainSingle(x => x.Action == "ReconciliationRecorded").Subject;
            evidence.TenantId.Should().Be(_tenant);
            evidence.CompanyId.Should().Be(_company);
            evidence.UserId.Should().Be(_user);
            evidence.UserName.Should().Be("Caso 32");
            evidence.OccurredAtUtc.Should().BeAfter(DateTime.UtcNow.AddMinutes(-5));
            evidence.FromState.Should().Be(ElectronicDocumentState.Signed);
            evidence.ToState.Should().Be(ElectronicDocumentState.Signed);
            evidence.Reason.Should().Contain("SRI en Línea caso 32").And.Contain("Sin resultado definitivo");
            audits.Should().NotContain(x => x.ToState == ElectronicDocumentState.Authorized
                || x.ToState == ElectronicDocumentState.Rejected);
        }
        authorization.Verify(x => x.CheckAsync(_company, Key, It.IsAny<CancellationToken>()), Times.Exactly(2));
        authorization.VerifyNoOtherCalls();
        reception.VerifyNoOtherCalls();
    }
}
