using FluentAssertions;
using ERP.Application.Common;
using ERP.Application.Modules.Sales.UseCases.CrearCliente;
using ERP.Application.Modules.Sales.UseCases.ListarClientes;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;

using Moq;
using ERP.Domain.MasterData.Interfaces;
using ERP.Application.Common.Persistence;
using ERP.Application.Common.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.Application.Tests.Customers;

public class CustomerHandlersIntegrationTests
{
    [Fact]
    public async Task CreateCustomerHandler_should_create_customer_for_current_tenant()
    {
        var subscriberId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var repo = new InMemoryCustomerRepository();
        var activity = new InMemoryUserActivityRepository();
        var tenant = new TestCurrentSubscriber(subscriberId);
        var company = new TestCurrentCompany(Guid.NewGuid());
        var user = new TestCurrentUser(userId);

        var bpRepo = new Mock<IBusinessPartnerRepository>(MockBehavior.Loose);
        var cpRepo = new Mock<ICustomerProfileRepository>(MockBehavior.Loose);
        var dbExceptions = new Mock<IDatabaseExceptionTranslator>(MockBehavior.Loose);
        var metrics = new Mock<ISecurityMetrics>(MockBehavior.Loose);
        var logger = NullLogger<CreateCustomerCommandHandler>.Instance;

        var handler = new CreateCustomerCommandHandler(
            repo, activity, tenant, company, user,
            bpRepo.Object, cpRepo.Object, dbExceptions.Object, metrics.Object, logger);

        var result = await handler.Handle(new CreateCustomerCommand(
            IdentificationType: "RUC",
            IdentificationNumber: "1234567890001",
            LegalName: "Cliente Integración",
            TradeName: null,
            AddressLine: "Av. Integración",
            Phone: "0991111111",
            Email: "integracion@test.local",
            Notes: null,
            IsActive: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.IdentificationType.Should().Be("RUC");
        result.Value.LegalName.Should().Be("Cliente Integración");

        var tenantCustomers = await repo.GetAsync(subscriberId, null, null, CancellationToken.None);
        tenantCustomers.Should().HaveCount(1);
        tenantCustomers[0].SubscriberId.Should().Be(subscriberId);
        activity.Activities.Should().ContainSingle();
    }

    [Fact]
    public async Task GetCustomersHandler_should_return_only_current_tenant_customers()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var repo = new InMemoryCustomerRepository();

        await repo.AddAsync(Customer.Create(
            tenantA,
            "RUC",
            "0999999999001",
            "Cliente A",
            null,
            null,
            null,
            null,
            null,
            userId));

        await repo.AddAsync(Customer.Create(
            tenantB,
            "CI",
            "1717171717",
            "Cliente B",
            null,
            null,
            null,
            null,
            null,
            userId));

        await repo.SaveChangesAsync(CancellationToken.None);

        var handler = new GetCustomersQueryHandler(repo, new TestCurrentSubscriber(tenantA));
        var result = await handler.Handle(new GetCustomersQuery(null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().HaveCount(1);
        result.Value[0].LegalName.Should().Be("Cliente A");
    }

    private sealed class TestCurrentSubscriber : ICurrentSubscriber
    {
        public TestCurrentSubscriber(Guid subscriberId) => SubscriberId = subscriberId;
        public Guid SubscriberId { get; }
        public bool IsAuthenticated => true;
    }

    private sealed class TestCurrentCompany : ICurrentCompany
    {
        public TestCurrentCompany(Guid companyId) => CompanyId = companyId;
        public Guid CompanyId { get; }
        public bool HasCompanyContext => true;
        public bool IsAuthenticated => true;
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public TestCurrentUser(Guid userId)
        {
            UserId = userId;
            Email = "integration@test.local";
            FullName = "Integration Test";
        }

        public Guid UserId { get; }
        public string? Email { get; }
        public string? FullName { get; }
        public string? Role => "Admin";
        public bool IsAuthenticated => true;
    }

    private sealed class InMemoryUserActivityRepository : IUserActivityRepository
    {
        public List<UserActivity> Activities { get; } = new();

        public Task AddAsync(UserActivity activity, CancellationToken ct = default)
        {
            Activities.Add(activity);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<UserActivity>> GetMyRecentAsync(
            Guid subscriberId,
            Guid userId,
            string? module = null,
            int skip = 0,
            int take = 50,
            CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<UserActivity>>(Activities);
        }

        public Task<IReadOnlyList<UserActivity>> GetByEntityAsync(
            Guid subscriberId,
            string entityType,
            Guid entityId,
            int take = 10,
            CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<UserActivity>>(Activities);
        }
    }

    private sealed class InMemoryCustomerRepository : ICustomerRepository
    {
        private readonly List<Customer> _customers = new();

        public Task AddAsync(Customer customer, CancellationToken ct = default)
        {
            _customers.Add(customer);
            return Task.CompletedTask;
        }

        public Task<Customer?> GetByIdAsync(Guid subscriberId, Guid id, CancellationToken ct = default)
        {
            var item = _customers.FirstOrDefault(x => x.SubscriberId == subscriberId && x.Id == id);
            return Task.FromResult(item);
        }

        public Task<bool> ExistsIdentificationAsync(
            Guid subscriberId,
            string identificationType,
            string identificationNumber,
            Guid? excludeCustomerId,
            CancellationToken ct = default)
        {
            var type = Customer.NormalizeIdentificationType(identificationType);
            var number = Customer.NormalizeIdentificationNumber(identificationNumber);

            var query = _customers.Where(x =>
                x.SubscriberId == subscriberId &&
                x.IdentificationType == type &&
                x.IdentificationNumber == number);

            if (excludeCustomerId is not null)
                query = query.Where(x => x.Id != excludeCustomerId.Value);

            return Task.FromResult(query.Any());
        }

        public Task<IReadOnlyList<Customer>> GetAsync(
            Guid subscriberId,
            bool? activeFilter,
            string? search,
            CancellationToken ct = default)
        {
            var query = _customers.Where(x => x.SubscriberId == subscriberId);
            if (activeFilter is true) query = query.Where(x => x.IsActive);
            if (activeFilter is false) query = query.Where(x => !x.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                query = query.Where(x =>
                    x.LegalName.ToLowerInvariant().Contains(term) ||
                    x.IdentificationNumber.ToLowerInvariant().Contains(term));
            }

            return Task.FromResult<IReadOnlyList<Customer>>(query.ToList());
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
