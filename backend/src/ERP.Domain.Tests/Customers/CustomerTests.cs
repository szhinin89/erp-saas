using FluentAssertions;
using ERP.Domain.Modules.Sales.Entities;

namespace ERP.Domain.Tests.Customers;

public class CustomerTests
{
    [Fact]
    public void Create_should_set_tenant_and_audit_fields()
    {
        var subscriberId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var customer = Customer.Create(
            subscriberId,
            "RUC",
            "1234567890001",
            "Cliente Demo",
            null,
            "Av. Principal",
            "0999999999",
            "demo@email.com",
            null,
            userId);

        customer.SubscriberId.Should().Be(subscriberId);
        customer.CreatedBy.Should().Be(userId);
        customer.IsActive.Should().BeTrue();
        customer.Email.Should().Be("demo@email.com");
    }

    [Fact]
    public void Create_should_throw_when_identification_type_is_invalid()
    {
        var act = () => Customer.Create(
            Guid.NewGuid(),
            "PASSPORT",
            "AA12345",
            "Cliente Demo",
            null,
            null,
            null,
            null,
            null,
            Guid.NewGuid());

        act.Should().Throw<ArgumentException>()
            .WithMessage("*RUC o CI*");
    }

    [Fact]
    public void Create_should_throw_when_email_format_is_invalid()
    {
        var act = () => Customer.Create(
            Guid.NewGuid(),
            "CI",
            "1717171717",
            "Cliente Demo",
            null,
            null,
            null,
            "correo-invalido",
            null,
            Guid.NewGuid());

        act.Should().Throw<ArgumentException>()
            .WithMessage("*correo*");
    }
}
