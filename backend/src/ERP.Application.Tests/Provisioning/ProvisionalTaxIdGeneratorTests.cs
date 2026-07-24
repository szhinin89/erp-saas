using ERP.Application.Common;
using FluentAssertions;

namespace ERP.Application.Tests.Provisioning;

public sealed class ProvisionalTaxIdGeneratorTests
{
    [Fact]
    public void Create_generates_tmp_ec_prefix_and_unique_values()
    {
        var a = ProvisionalTaxIdGenerator.Create();
        var b = ProvisionalTaxIdGenerator.Create();

        a.Should().StartWith("TMP-EC-");
        b.Should().StartWith("TMP-EC-");
        a.Should().NotBe(b);
        a.Length.Should().Be(15);
    }
}
