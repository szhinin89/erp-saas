using FluentAssertions;

namespace ERP.Architecture.Tests;

/// <summary>
/// ZH-API-THIN-BP-ROLES-01 — BusinessPartnerRolesController se limita a Request → Command →
/// mediator → ApiResult. La construcción de SupplierRoleConfig/CarrierRoleConfig/CustomerRoleConfig
/// y la traducción de sus invariantes viven en Application (RoleConfigFactory + handlers); este
/// test evita que vuelvan al controller.
/// </summary>
public sealed class BusinessPartnerRolesControllerThinTests
{
    [Fact]
    public void BusinessPartnerRolesController_no_construye_value_objects_de_Domain()
    {
        var file = Path.Combine(ResolveBackendSrcRoot(), "ERP.API", "Controllers", "BusinessPartnerRolesController.cs");
        File.Exists(file).Should().BeTrue();
        var text = File.ReadAllText(file);

        var forbidden = new[]
        {
            "ERP.Domain.MasterData.ValueObjects",
            "SupplierRoleConfig.Create(",
            "CarrierRoleConfig.Create(",
            "CustomerRoleConfig.Create(",
            "catch (ArgumentException",
        };

        forbidden
            .Where(marker => text.Contains(marker, StringComparison.Ordinal))
            .Should()
            .BeEmpty("la config de rol se construye y valida en Application (RoleConfigFactory), no en el controller");
    }

    private static string ResolveBackendSrcRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "ERP.API")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("No se encontró backend/src (ERP.API).");
    }
}
