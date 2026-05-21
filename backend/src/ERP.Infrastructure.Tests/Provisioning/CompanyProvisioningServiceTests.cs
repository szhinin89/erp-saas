using ERP.Application.Common;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Subscribers.Entities;
using ERP.Application.Subscriptions.CommercialPlanLimits;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Moq;

namespace ERP.Infrastructure.Tests.Provisioning;

public sealed class CompanyProvisioningServiceTests
{
    [Fact]
    public async Task CreateDefaultCompanyForSubscriberAsync_without_ruc_generates_unique_provisional()
    {
        var companies = new Mock<ICompanyRepository>(MockBehavior.Strict);
        companies.Setup(r => r.GetByRucAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);

        var sut = new CompanyProvisioningService(
            companies.Object,
            Mock.Of<ICommercialPlanLimitService>(),
            Mock.Of<IAccessRepository>());

        var subscriber = Subscriber.Create("ZH Tech", "zh", Guid.NewGuid());
        var company = await sut.CreateDefaultCompanyForSubscriberAsync(subscriber);

        company.IsProvisionalTaxId.Should().BeTrue();
        company.TaxIdStatus.Should().Be(TaxIdStatus.Pending);
        company.Ruc.Should().StartWith("TMP-EC-");
    }

    [Fact]
    public async Task CreateDefaultCompanyForSubscriberAsync_duplicate_ruc_throws_domain_exception()
    {
        var existing = Company.CreateManaged(Guid.NewGuid(), "1791234567001", "Existing", "Addr");
        var companies = new Mock<ICompanyRepository>(MockBehavior.Strict);
        companies.Setup(r => r.GetByRucAsync("1791234567001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var sut = new CompanyProvisioningService(
            companies.Object,
            Mock.Of<ICommercialPlanLimitService>(),
            Mock.Of<IAccessRepository>());

        var subscriber = Subscriber.Create("Other", "other", Guid.NewGuid(), ruc: "1791234567001");
        var act = () => sut.CreateDefaultCompanyForSubscriberAsync(subscriber);

        await act.Should().ThrowAsync<CompanyRucAlreadyExistsException>()
            .WithMessage("*1791234567001*");
    }
}
