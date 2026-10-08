using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.SchemaValidation;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.Sales.Services;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.ValueObjects;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.ElectronicDocuments;
using ERP.Infrastructure.Persistence.Repositories.Sales;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.Infrastructure.Tests.Accounting;

public sealed partial class SalesInvoiceAuthorizedPostingIntegrationTests
{
    private async Task<Guid> SeedA9Async(SalesInvoiceStatus status = SalesInvoiceStatus.Authorized,
        EmissionType emission = EmissionType.Electronic, string number = "001-001-000000099",
        A9Owner? owner = null)
    {
        var tid = owner?.TenantId ?? _tenantId;
        var cid = owner?.CompanyId ?? _companyId;
        await using var db = A9Context(tid, cid);
        var code = $"A9-{Guid.NewGuid():N}"[..16];
        var term = PaymentTerm.Create(tid, code, "Contado", 1, 0, _createdBy);
        var method = PaymentMethod.Create(tid, code, "Efectivo", false, false, 0, _createdBy);
        db.PaymentTerms.Add(term);
        db.PaymentMethods.Add(method);
        var ep = await db.EmissionPoints.SingleAsync();
        var invoice = SalesInvoice.CreateDraft(tid, cid, owner?.BranchId ?? _branchId, owner?.CustomerId ?? _customerId,
            CustomerSnapshot.Create("Cliente Test", "1710034065", "05"), number,
            new DateOnly(2026, 7, 25), _createdBy,
            PaymentTermSnapshot.Create(term.Id, term.Name, 1, 0), owner?.CashSessionId ?? _cashSessionId,
            emission, emissionPointId: ep.Id, sriPaymentMethodCode: "01");
        invoice.ReplaceLines([SalesInvoiceDetail.Create(invoice.Id, tid, "Servicio A9",
            1m, 10m, "10", "UNIT")], _createdBy);
        invoice.ReplacePayments([SalesInvoicePayment.Create(invoice.Id, tid, method.Id,
            "01", "Efectivo", 10m)], _createdBy);
        if (status != SalesInvoiceStatus.Draft) invoice.Authorize(_createdBy);
        if (status == SalesInvoiceStatus.Cancelled) invoice.Cancel("A9 exclusion", _createdBy);
        db.SalesInvoices.Add(invoice);
        await db.SaveChangesAsync();
        return invoice.Id;
    }

    private ErpDbContext A9Context(Guid? tenant = null, Guid? company = null, IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString());
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new(options.Options, new FixedCurrentTenant(tenant ?? _tenantId), new NoOpPublisher(),
            new FixedCurrentCompany(company ?? _companyId));
    }

    private ElectronicDocumentIssuer A9Issuer(ErpDbContext db, bool originalRequest = false,
        Action? pipelineEntered = null)
    {
        var resolver = new Mock<IElectronicDocumentXmlSupplierResolver>(MockBehavior.Strict);
        if (originalRequest)
        {
            var supplier = new Mock<IElectronicDocumentXmlSupplier>();
            supplier.Setup(s => s.BuildXmlAsync(It.IsAny<ElectronicDocumentSourceReference>(),
                    It.IsAny<CancellationToken>()))
                .Callback(() => pipelineEntered?.Invoke())
                .ReturnsAsync(Result<ElectronicDocumentXml>.Failure("A9 transport boundary"));
            resolver.Setup(r => r.Resolve(ElectronicDocumentType.Invoice)).Returns(supplier.Object);
        }
        var guards = new Mock<IElectronicDocumentSourceLifecycleGuardResolver>();
        return new(new ElectronicDocumentRepository(db, Mock.Of<ERP.Application.Common.Services.ICompanyClock>()),
            resolver.Object, Mock.Of<IElectronicDocumentSchemaValidatorResolver>(MockBehavior.Strict),
            Mock.Of<IElectronicDocumentSigningService>(MockBehavior.Strict),
            Mock.Of<IElectronicDocumentXmlStorageService>(MockBehavior.Strict),
            Mock.Of<IElectronicDocumentReceptionService>(MockBehavior.Strict),
            Mock.Of<IElectronicDocumentAuthorizationService>(MockBehavior.Strict),
            Mock.Of<IFileStorage>(MockBehavior.Strict), new PostgresDatabaseExceptionTranslator(),
            guards.Object, new UnitOfWork(db), NullLogger<ElectronicDocumentIssuer>.Instance);
    }

    private SalesElectronicDocumentRecovery A9Recovery(ErpDbContext db,
        Guid? tenant = null, Guid? company = null) => new(db,
            new FixedCurrentTenant(tenant ?? _tenantId), new FixedCurrentCompany(company ?? _companyId), A9Issuer(db));

    private SalesElectronicRecoveryCandidate A9Candidate(Guid id) => new(_tenantId, _companyId, _branchId, id);

    private sealed record A9Owner(Guid TenantId, Guid CompanyId, Guid BranchId, Guid CustomerId, Guid CashSessionId);

    private async Task<A9Owner> SeedA9OwnerAsync(Guid? tenantId = null)
    {
        await using var db = A9Context(Guid.Empty, Guid.Empty);
        if (!tenantId.HasValue)
        {
            var tenant = Tenant.Create("A9 tenant", $"a9-{Guid.NewGuid():N}"[..16], _createdBy);
            db.Tenants.Add(tenant); tenantId = tenant.Id;
        }
        var company = Company.CreateManaged(tenantId.Value,
            tenantId == _tenantId ? "1790098765001" : "1791234567001", "A9 Company", createdBy: _createdBy);
        var branch = Branch.Create(tenantId.Value, "A9 Branch", "A9 Address", "001",
            description: null, reference: null, postalCode: null, phone: null, secondaryPhone: null,
            email: null, website: null, managerName: null, managerPosition: null, managerEmail: null,
            managerPhone: null, countryId: null, provinceId: null, cantonId: null, parishId: null,
            latitude: null, longitude: null, openingDate: null, internalNotes: null,
            isMainBranch: true, createdBy: _createdBy, companyId: company.Id);
        var customer = BusinessPartner.Create(tenantId.Value, "05", "1710034065", 1, "A9 Customer", _createdBy);
        // Within the existing tenant use its real tenant-wide customer.
        var customerId = tenantId == _tenantId ? _customerId : customer.Id;
        if (tenantId != _tenantId) db.BusinessPartners.Add(customer);
        var establishment = Establishment.Create(tenantId.Value, branch.Id, company.Id, "001", "A9",
            "A9 Address", null, true, _createdBy);
        var register = CashRegister.Create(tenantId.Value, company.Id, branch.Id, "A9", "A9", _createdBy);
        db.Companies.Add(company); db.Branches.Add(branch); db.Establishments.Add(establishment);
        db.CashRegisters.Add(register);
        await db.SaveChangesAsync();
        var ep = EmissionPoint.Create(tenantId.Value, company.Id, establishment.Id, "001", "A9",
            EmissionType.Electronic, true, _createdBy);
        db.EmissionPoints.Add(ep); await db.SaveChangesAsync();
        var session = CashSession.Open(tenantId.Value, company.Id, branch.Id, Guid.NewGuid(), register.Id,
            "A9", "A9", ep.Id, "001", 0m, _createdBy);
        db.CashSessions.Add(session); await db.SaveChangesAsync();
        return new(tenantId.Value, company.Id, branch.Id, customerId, session.Id);
    }

    [Fact]
    public async Task A9_committed_sale_without_document_survives_authorization_retry_then_recovers_in_new_context()
    {
        var id = await SeedA9Async();
        // Reproduce the original hole: a repeated authorization succeeds but cannot create the missing document.
        (await A8AttemptAsync(id)).IsSuccess.Should().BeTrue();
        await using (var before = CreateContext())
            (await before.ElectronicDocuments.CountAsync()).Should().Be(0);
        await using (var discovery = A9Context(Guid.Empty, Guid.Empty))
            (await A9Recovery(discovery, Guid.Empty, Guid.Empty).GetCandidatesAsync(default))
                .Should().ContainSingle(c => c == A9Candidate(id));
        // All request/discovery state has gone away; only persisted PostgreSQL state is retained.
        await using (var restarted = A9Context())
            await A9Recovery(restarted).RecoverAsync(A9Candidate(id), default);
        await using var verify = CreateContext();
        var document = await verify.ElectronicDocuments.SingleAsync();
        document.SourceEntityId.Should().Be(id);
        document.CurrentState.Should().Be(ElectronicDocumentState.Draft);
        (await verify.SalesInvoices.SingleAsync()).InvoiceNumber.Should().Be("001-001-000000099");
    }

    [Fact]
    public async Task A9_repeated_runs_do_not_repeat_commercial_effects_or_registration()
    {
        var id = await SeedA9Async();
        await using var before = CreateContext();
        var originalInvoice = await before.SalesInvoices.AsNoTracking().SingleAsync();
        var originalEvents = await before.OutboxMessages.Select(m => m.Id).ToArrayAsync();
        for (var n = 0; n < 3; n++)
        {
            await using var db = A9Context();
            await A9Recovery(db).RecoverAsync(A9Candidate(id), default);
        }
        await using var verify = CreateContext();
        (await verify.ElectronicDocuments.CountAsync()).Should().Be(1);
        (await A9Recovery(verify).GetCandidatesAsync(default)).Should().BeEmpty();
        var invoice = await verify.SalesInvoices.AsNoTracking().SingleAsync();
        invoice.Should().BeEquivalentTo(originalInvoice);
        (await verify.OutboxMessages.CountAsync(m => originalEvents.Contains(m.Id))).Should().Be(originalEvents.Length);
        (await verify.OutboxMessages.CountAsync()).Should().Be(originalEvents.Length + 1);
        (await verify.StockMovements.CountAsync()).Should().Be(0);
        (await verify.SalesReceivables.CountAsync()).Should().Be(0);
        (await verify.JournalEntries.CountAsync()).Should().Be(0);
        (await verify.DocumentSequences.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(ElectronicDocumentState.Draft)]
    [InlineData(ElectronicDocumentState.Failed)]
    [InlineData(ElectronicDocumentState.Signed)]
    [InlineData(ElectronicDocumentState.Received)]
    [InlineData(ElectronicDocumentState.Authorized)]
    public async Task A9_existing_document_never_runs_pipeline_even_with_stale_candidate(ElectronicDocumentState state)
    {
        var id = await SeedA9Async();
        await using (var seed = CreateContext())
        {
            var document = ElectronicDocument.Create(_tenantId, _companyId, ElectronicDocumentType.Invoice,
                "Sales", id, _createdBy);
            if (state == ElectronicDocumentState.Failed) document.MarkFailed("A9 failure", _createdBy);
            if (state is ElectronicDocumentState.Signed or ElectronicDocumentState.Received or ElectronicDocumentState.Authorized)
            {
                document.MarkXmlGenerated("draft.xml", "1.1.0", "1.1.0", _createdBy);
                document.MarkSigned("signed.xml", AccessKey.Create(new string('1', 49)), _createdBy);
                if (state != ElectronicDocumentState.Signed)
                {
                    document.MarkSent(_createdBy); document.MarkReceived(_createdBy);
                }
                if (state == ElectronicDocumentState.Authorized)
                    document.MarkAuthorized(AuthorizationNumber.Create(new string('1', 49)), DateTime.UtcNow,
                        "authorized.xml", _createdBy);
            }
            seed.ElectronicDocuments.Add(document);
            await seed.SaveChangesAsync();
        }
        await using var db = A9Context();
        var before = await db.ElectronicDocuments.AsNoTracking().SingleAsync();
        (await A9Recovery(db).GetCandidatesAsync(default)).Should().BeEmpty();
        await A9Recovery(db).RecoverAsync(A9Candidate(id), default);
        // Exercise the inner check too: document appeared after the reconciler's outer check.
        var result = await A9Issuer(db).RegisterMissingAsync(new(_tenantId, _companyId,
            ElectronicDocumentType.Invoice, "Sales", id, _createdBy));
        result.IsSuccess.Should().BeTrue();
        (await db.ElectronicDocuments.AsNoTracking().SingleAsync()).Should().BeEquivalentTo(before);
    }

    [Theory]
    [InlineData(SalesInvoiceStatus.Draft, EmissionType.Electronic, "001-001-000000099")]
    [InlineData(SalesInvoiceStatus.Cancelled, EmissionType.Electronic, "001-001-000000099")]
    [InlineData(SalesInvoiceStatus.Authorized, EmissionType.Physical, "001-001-000000099")]
    [InlineData(SalesInvoiceStatus.Authorized, EmissionType.Electronic, "DRAFT-A9")]
    [InlineData(SalesInvoiceStatus.Authorized, EmissionType.Electronic, "001-001-000000000")]
    public async Task A9_ineligible_sale_is_excluded_and_revalidated(SalesInvoiceStatus status, EmissionType emission, string number)
    {
        var id = await SeedA9Async(status, emission, number);
        await using var db = A9Context();
        (await A9Recovery(db).GetCandidatesAsync(default)).Should().BeEmpty();
        await A9Recovery(db).RecoverAsync(A9Candidate(id), default);
        (await db.ElectronicDocuments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A9_wrong_tenant_company_branch_and_absent_context_cannot_create()
    {
        var id = await SeedA9Async();
        var candidate = A9Candidate(id);
        foreach (var wrong in new[] { candidate with { TenantId = Guid.NewGuid() },
            candidate with { CompanyId = Guid.NewGuid() }, candidate with { BranchId = Guid.NewGuid() } })
        {
            await using var db = A9Context();
            await A9Recovery(db).RecoverAsync(wrong, default);
        }
        foreach (var scope in new[] { (Guid.Empty, Guid.Empty), (Guid.NewGuid(), _companyId), (_tenantId, Guid.NewGuid()) })
        {
            await using var db = A9Context(scope.Item1, scope.Item2);
            await A9Recovery(db, scope.Item1, scope.Item2).RecoverAsync(candidate, default);
        }
        await using var verify = CreateContext();
        (await verify.ElectronicDocuments.CountAsync()).Should().Be(0);
        await A9Recovery(verify).RecoverAsync(candidate, default);
        (await verify.ElectronicDocuments.SingleAsync()).CompanyId.Should().Be(_companyId);
    }

    [Fact]
    public async Task A9_discovery_across_real_tenants_companies_and_branches_recovers_only_each_owner()
    {
        var first = await SeedA9Async();
        var secondOwner = await SeedA9OwnerAsync(_tenantId);
        var thirdOwner = await SeedA9OwnerAsync();
        var second = await SeedA9Async(owner: secondOwner);
        var third = await SeedA9Async(owner: thirdOwner);
        IReadOnlyList<SalesElectronicRecoveryCandidate> candidates;
        await using (var discovery = A9Context(Guid.Empty, Guid.Empty))
            candidates = await A9Recovery(discovery, Guid.Empty, Guid.Empty).GetCandidatesAsync(default);
        candidates.Should().HaveCount(3);
        foreach (var candidate in candidates)
        {
            // A different company's scoped filters must not expose or mutate the source.
            var wrongCompany = candidate.CompanyId == _companyId ? secondOwner.CompanyId : _companyId;
            await using (var wrong = A9Context(candidate.TenantId, wrongCompany))
                await A9Recovery(wrong, candidate.TenantId, wrongCompany).RecoverAsync(candidate, default);
            await using var correct = A9Context(candidate.TenantId, candidate.CompanyId);
            await A9Recovery(correct, candidate.TenantId, candidate.CompanyId).RecoverAsync(candidate, default);
            var document = await correct.ElectronicDocuments.SingleAsync();
            document.SourceEntityId.Should().Be(candidate.InvoiceId);
            document.TenantId.Should().Be(candidate.TenantId);
            document.CompanyId.Should().Be(candidate.CompanyId);
            (await correct.SalesInvoices.SingleAsync()).BranchId.Should().Be(candidate.BranchId);
        }
        candidates.Select(c => c.InvoiceId).Should().BeEquivalentTo([first, second, third]);
    }

    [Fact]
    public async Task A9_cancelled_after_discovery_is_not_registered()
    {
        var id = await SeedA9Async();
        await using (var discovery = A9Context())
            (await A9Recovery(discovery).GetCandidatesAsync(default)).Should().ContainSingle();
        await using (var cancel = CreateContext())
        {
            (await cancel.SalesInvoices.SingleAsync()).Cancel("A9 stale candidate", _createdBy);
            await cancel.SaveChangesAsync();
        }
        await using var db = A9Context();
        await A9Recovery(db).RecoverAsync(A9Candidate(id), default);
        (await db.ElectronicDocuments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A9_missing_emission_point_is_not_registered()
    {
        var id = await SeedA9Async();
        await using var db = A9Context();
        await db.SalesInvoices.Where(i => i.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.EmissionPointId, (Guid?)null));
        (await A9Recovery(db).GetCandidatesAsync(default)).Should().BeEmpty();
        await A9Recovery(db).RecoverAsync(A9Candidate(id), default);
        (await db.ElectronicDocuments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A9_two_reconcilers_race_at_insert_and_create_exactly_one()
    {
        var id = await SeedA9Async();
        var gate = new A9InsertGate();
        async Task Attempt()
        {
            await using var db = A9Context(interceptor: gate);
            await A9Recovery(db).RecoverAsync(A9Candidate(id), default);
        }
        await Task.WhenAll(Attempt(), Attempt()).WaitAsync(TimeSpan.FromSeconds(30));
        gate.Arrivals.Should().Be(2, "both issuers read absence before competing on the unique constraint");
        await using var verify = CreateContext();
        (await verify.ElectronicDocuments.CountAsync()).Should().Be(1);
        (await verify.OutboxMessages.CountAsync(m => m.EventName == "ElectronicDocumentCreatedEvent")).Should().Be(1);
    }

    [Fact]
    public async Task A9_reconciler_races_original_request_registration_without_duplicate()
    {
        var id = await SeedA9Async();
        var gate = new A9InsertGate();
        var pipelineCalls = 0;
        async Task Recover()
        {
            await using var db = A9Context(interceptor: gate);
            await A9Recovery(db).RecoverAsync(A9Candidate(id), default);
        }
        async Task Original()
        {
            await using var db = A9Context(interceptor: gate);
            await A9Issuer(db, originalRequest: true, () => Interlocked.Increment(ref pipelineCalls))
                .RegisterAsync(new(_tenantId, _companyId, ElectronicDocumentType.Invoice, "Sales", id, _createdBy));
        }
        await Task.WhenAll(Recover(), Original()).WaitAsync(TimeSpan.FromSeconds(30));
        gate.Arrivals.Should().Be(2);
        pipelineCalls.Should().BeInRange(0, 1);
        await using var verify = CreateContext();
        (await verify.ElectronicDocuments.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task A9_failure_before_insert_leaves_durable_candidate_for_next_execution()
    {
        var id = await SeedA9Async();
        await using (var failing = A9Context(interceptor: new A9FailBeforeInsert()))
            await FluentActions.Awaiting(() => A9Recovery(failing).RecoverAsync(A9Candidate(id), default))
                .Should().ThrowAsync<InvalidOperationException>();
        await using var restarted = A9Context();
        (await A9Recovery(restarted).GetCandidatesAsync(default)).Should().ContainSingle();
        await A9Recovery(restarted).RecoverAsync(A9Candidate(id), default);
        (await restarted.ElectronicDocuments.CountAsync()).Should().Be(1);
    }

    private sealed class A9InsertGate : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _both = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;
        public int Arrivals => _arrivals;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (data.Context!.ChangeTracker.Entries<ElectronicDocument>().Any(e => e.State == EntityState.Added))
            {
                if (Interlocked.Increment(ref _arrivals) == 2) _both.TrySetResult();
                await _both.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            }
            return result;
        }
    }

    private sealed class A9FailBeforeInsert : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("A9 injected crash before persist");
    }
}
