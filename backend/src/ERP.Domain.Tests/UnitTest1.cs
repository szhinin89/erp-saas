using FluentAssertions;
using ERP.Domain.Accounting.ValueObjects;
using ERP.Domain.Auth.ValueObjects;

namespace ERP.Domain.Tests;

public class MoneyTests
{
    [Fact]
    public void Ctor_should_normalize_currency_to_uppercase()
    {
        var money = new Money(10m, "usd");

        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void Add_should_throw_when_currency_differs()
    {
        var a = new Money(10m, "USD");
        var b = new Money(5m, "EUR");

        a.Invoking(x => x.Add(b))
            .Should()
            .Throw<InvalidOperationException>();
    }
}

public class EmailTests
{
    [Fact]
    public void Ctor_should_trim_and_lowercase()
    {
        var email = new Email("  TEST@Example.com ");

        email.Value.Should().Be("test@example.com");
    }

    [Fact]
    public void Ctor_should_throw_when_missing_at()
    {
        Action act = () => _ = new Email("not-an-email");

        act.Should().Throw<ArgumentException>();
    }
}
