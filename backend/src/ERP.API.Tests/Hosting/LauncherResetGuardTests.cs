using ERP.API.Hosting;
using FluentAssertions;

namespace ERP.API.Tests.Hosting;

public sealed class LauncherResetGuardTests
{
    [Theory]
    [InlineData("remote", 5435, "erp_zh_dev", "Development")]
    [InlineData("localhost", 5432, "erp_zh_dev", "Development")]
    [InlineData("localhost", 5435, "erp_zh_test", "Development")]
    [InlineData("localhost", 5435, "erp_zh_prod", "Development")]
    [InlineData("localhost", 5435, "erp_zh_dev", "Production")]
    [InlineData("localhost,remote", 5435, "erp_zh_dev", "Development")]
    public void Unauthorized_target_is_rejected_before_confirmation(string host, int port, string database, string environment)
    {
        var prompted = false;
        var act = () => LauncherResetGuard.Confirm(
            $"Host={host};Port={port};Database={database}", environment,
            _ => { }, () => { prompted = true; return null; });
        act.Should().Throw<InvalidOperationException>();
        prompted.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("RESETEAR")]
    [InlineData("eliminar localhost:5435/erp_zh_dev Development")]
    public void Missing_or_inexact_confirmation_is_rejected(string? confirmation)
    {
        var act = () => LauncherResetGuard.Confirm(
            "Host=localhost;Port=5435;Database=erp_zh_dev", "Development",
            _ => { }, () => confirmation);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Exact_confirmation_accepts_only_the_displayed_target()
    {
        var messages = new List<string>();
        LauncherResetGuard.Confirm("Host=localhost;Port=5435;Database=erp_zh_dev", "Development",
            messages.Add, () => {
                messages[0].Should().Contain("localhost").And.Contain("5435").And.Contain("erp_zh_dev").And.Contain("Development");
                return "ELIMINAR localhost:5435/erp_zh_dev Development";
            });
    }
}
