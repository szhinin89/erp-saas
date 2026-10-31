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

    private ServiceProvider BuildProvider(
        Mock<IElectronicDocumentAuthorizationService> authorization,
        Mock<IElectronicDocumentReceptionService> reception)
    {
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
        return services.BuildServiceProvider();
    }

    private void SetContext(IServiceProvider provider, Guid tenant, Guid company)
    {
        var accessor = provider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext();
        accessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [new Claim("tenant_id", tenant.ToString()),
         new Claim(ClaimTypes.NameIdentifier, _user.ToString()),
         new Claim(ClaimTypes.Name, "Caso 32")], "Test"));
        accessor.HttpContext.Request.Headers["X-Company-Id"] = company.ToString();
    }

    private async Task SeedScopeAsync(IServiceProvider provider)
    {
        var seedActor = Guid.NewGuid();
        var seedTenant = Tenant.Create("Caso 32", "case32", seedActor);
        var seedCompany = Company.CreateManaged(seedTenant.Id, "1790012345001", "Caso 32", createdBy: seedActor);
        var user = IdentityUser.Create("case32", "Caso", "32", null, "TEST_ONLY_HASH", seedActor);
        _tenant = seedTenant.Id;
        _company = seedCompany.Id;
        _user = user.Id;
        SetContext(provider, _tenant, _company);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await db.Database.MigrateAsync();
        db.Tenants.Add(seedTenant);
        db.Companies.Add(seedCompany);
        db.IdentityUsers.Add(user);
        db.CompanyUserMemberships.Add(CompanyUserMembership.Create(_company, _user, "User", null, seedActor));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Signed_unknown_outcome_is_only_queried_and_reconciliation_is_audited_with_scope_isolation()
    {
        var authorization = new Mock<IElectronicDocumentAuthorizationService>(MockBehavior.Strict);
        var reception = new Mock<IElectronicDocumentReceptionService>(MockBehavior.Strict);
        await using var provider = BuildProvider(authorization, reception);
        await SeedScopeAsync(provider);
        this.SetContext(provider, _tenant, _company); // HttpContext is AsyncLocal
        authorization.Setup(x => x.CheckAsync(_company, Key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SriAuthorizationResult>.Failure("SRI sin respuesta"));
        void SetContext(Guid tenant, Guid company) => this.SetContext(provider, tenant, company);
        Guid id;
        await using (var scope = provider.CreateAsyncScope())
        {
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

    private const string RejectedKey = "2222222222222222222222222222222222222222222222222";
    private const string ReceivedKey = "3333333333333333333333333333333333333333333333333";

    // R11: the SRI may answer up to 24 h after RECIBIDA (§7.5). A Received document keeps being queried
    // past the company maximum until 24 h from its persisted Received milestone; then DeadLetter with
    // reconciliation pending. A Signed document has no reception evidence: no computable window, it keeps
    // being queried (fail-closed). Never Recepción, never a new key, never Rejected by timeout.
    [Fact]
    public async Task Sri_outcome_pending_is_queried_for_the_24h_window_across_worker_restarts_then_requires_reconciliation()
    {
        var authorization = new Mock<IElectronicDocumentAuthorizationService>(MockBehavior.Strict);
        var reception = new Mock<IElectronicDocumentReceptionService>(MockBehavior.Strict);
        Guid pendingId, rejectedId, receivedId;
        var overMaximum = ERP.Domain.Configuration.Definitions.Modules.ElectronicDocumentRetryMaximum.Default + 2;
        await using (var provider = BuildProvider(authorization, reception))
        {
            await SeedScopeAsync(provider);
            SetContext(provider, _tenant, _company); // HttpContext is AsyncLocal
            authorization.Setup(x => x.CheckAsync(_company, Key, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<SriAuthorizationResult>.Success(new SriAuthorizationResult { Status = "TIMEOUT" }));
            authorization.Setup(x => x.CheckAsync(_company, ReceivedKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<SriAuthorizationResult>.Failure("SRI sin respuesta"));
            authorization.Setup(x => x.CheckAsync(_company, RejectedKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<SriAuthorizationResult>.Success(new SriAuthorizationResult { Status = "NO AUTORIZADO" }));
            await using (var scope = provider.CreateAsyncScope())
            {
                ElectronicDocument Signed(string key)
                {
                    var document = ElectronicDocument.Create(_tenant, _company,
                        ElectronicDocumentType.Invoice, "Sales", Guid.NewGuid(), _user);
                    document.MarkXmlGenerated("draft.xml", "1.1.0", "1.1.0", _user);
                    document.MarkSigned("signed.xml", AccessKey.Create(key), _user);
                    for (var i = 0; i < overMaximum; i++)
                        document.MarkRetryAttempted(_user);
                    return document;
                }
                var pending = Signed(Key);
                var rejected = Signed(RejectedKey);
                var received = Signed(ReceivedKey);
                received.MarkSent(_user);
                received.MarkReceived(_user);
                pendingId = pending.Id;
                rejectedId = rejected.Id;
                receivedId = received.Id;
                var repository = scope.ServiceProvider.GetRequiredService<IElectronicDocumentRepository>();
                await repository.AddAsync(pending);
                await repository.AddAsync(rejected);
                await repository.AddAsync(received);
                await repository.SaveChangesAsync();
            }

            // Concurrent queries on the same document (worker + Monitor): no Recepción, no corruption.
            var outcomes = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
            {
                await using var scope = provider.CreateAsyncScope();
                try
                {
                    return (await scope.ServiceProvider.GetRequiredService<IElectronicDocumentIssuer>()
                        .RetryAsync(_tenant, pendingId, _user)).IsSuccess;
                }
                catch (DbUpdateConcurrencyException)
                {
                    return false;
                }
            }));
            outcomes.Should().Contain(true);

            foreach (var id in new[] { rejectedId, receivedId })
            {
                await using var scope = provider.CreateAsyncScope();
                (await scope.ServiceProvider.GetRequiredService<IElectronicDocumentIssuer>()
                    .RetryAsync(_tenant, id, _user)).IsSuccess.Should().BeTrue();
            }
        }

        await using (var provider = BuildProvider(authorization, reception))
        {
            SetContext(provider, _tenant, _company);
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
                var pending = await db.ElectronicDocuments.AsNoTracking().SingleAsync(x => x.Id == pendingId);
                pending.CurrentState.Should().Be(ElectronicDocumentState.Signed);
                pending.RetryCount.Should().BeGreaterThan(overMaximum);
                (await db.ElectronicDocuments.AsNoTracking().SingleAsync(x => x.Id == rejectedId))
                    .CurrentState.Should().Be(ElectronicDocumentState.Rejected);
                (await db.ElectronicDocuments.AsNoTracking().SingleAsync(x => x.Id == receivedId))
                    .CurrentState.Should().Be(ElectronicDocumentState.Received, "24 h from RECIBIDA not elapsed");

                // The worker restarted (new provider); 25 h elapse on every persisted milestone.
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE electronic_document_audit SET occurred_at_utc = occurred_at_utc - interval '25 hours' WHERE entity_id IN ({pendingId}, {receivedId})");
                var repository = scope.ServiceProvider.GetRequiredService<IElectronicDocumentRepository>();
                (await repository.GetFirstReceivedAtUtcAsync(_tenant, receivedId))
                    .Should().BeBefore(DateTime.UtcNow.AddHours(-24));
                (await repository.GetFirstReceivedAtUtcAsync(_tenant, pendingId))
                    .Should().BeNull("no reception evidence for a Signed document");
            }
            foreach (var id in new[] { pendingId, receivedId })
            {
                await using var scope = provider.CreateAsyncScope();
                (await scope.ServiceProvider.GetRequiredService<IElectronicDocumentIssuer>()
                    .RetryAsync(_tenant, id, _user)).IsSuccess.Should().BeTrue();
            }
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
                var received = await db.ElectronicDocuments.AsNoTracking().SingleAsync(x => x.Id == receivedId);
                received.CurrentState.Should().Be(ElectronicDocumentState.DeadLetter);
                received.PreDeadLetterState.Should().Be(ElectronicDocumentState.Received);
                received.RequiresSriReconciliation.Should().BeTrue();
                received.AccessKey!.Value.Should().Be(ReceivedKey);
                received.AuthorizationNumber.Should().BeNull();
                received.LastError.Should().Contain("24 h");

                var pending = await db.ElectronicDocuments.AsNoTracking().SingleAsync(x => x.Id == pendingId);
                pending.CurrentState.Should().Be(ElectronicDocumentState.Signed, "fail-closed: no DeadLetter by an assumed date");
                pending.RequiresSriReconciliation.Should().BeTrue();
                pending.AccessKey!.Value.Should().Be(Key);
            }

            // Another tenant/company cannot read the window or act on the document.
            SetContext(provider, Guid.NewGuid(), _company);
            await using (var scope = provider.CreateAsyncScope())
            {
                (await scope.ServiceProvider.GetRequiredService<IElectronicDocumentRepository>()
                    .GetFirstReceivedAtUtcAsync(_tenant, receivedId)).Should().BeNull();
            }
        }

        reception.VerifyNoOtherCalls();
    }
}
