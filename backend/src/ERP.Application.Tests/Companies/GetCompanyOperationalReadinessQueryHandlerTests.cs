using System.Reflection;
using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Application.Modules.Companies.UseCases.GetCompanyOperationalReadiness;
using ERP.Domain.Modules.Company.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Companies;

/// <summary>
/// COMPANY-OPERATING-SETUP-01 — el handler solo mapea el resultado del resolver (Domain) al DTO
/// de Application; toda la lógica de negocio real vive y se prueba en
/// CompanyOperationalReadinessResolverTests (ERP.Infrastructure.Tests).
/// </summary>
public sealed class GetCompanyOperationalReadinessQueryHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<ICompanyOperationalReadinessResolver> Resolver { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();

        public Fixture()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
        }

        public GetCompanyOperationalReadinessQueryHandler BuildHandler() =>
            new(Resolver.Object, Tenant.Object, Company.Object);
    }

    [Fact]
    public async Task Handle_mapea_el_resultado_del_resolver_al_dto_preservando_estructura()
    {
        var f = new Fixture();
        var domainResult = new CompanyOperationalReadinessResult(
            OverallStatus: ReadinessStatus.Warning,
            CanSell: true,
            CanIssueElectronicInvoices: false,
            CanUseInventory: true,
            CanUseCashRegister: false,
            Sections: new List<ReadinessSection>
            {
                new(
                    "identity",
                    ReadinessStatus.Ready,
                    new List<ReadinessItem>
                    {
                        new(
                            "identity.taxId",
                            ReadinessStatus.Ready,
                            ReadinessSeverity.Blocking,
                            ReadinessBlockingArea.Sales,
                            ReadinessActionTarget.CompanyProfile
                        ),
                    }
                ),
            }
        );

        f.Resolver
            .Setup(r => r.GetAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(domainResult);

        var result = await f.BuildHandler().Handle(new GetCompanyOperationalReadinessQuery(), default);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value!;
        dto.OverallStatus.Should().Be("Warning");
        dto.CanSell.Should().BeTrue();
        dto.CanIssueElectronicInvoices.Should().BeFalse();
        dto.CanUseInventory.Should().BeTrue();
        dto.CanUseCashRegister.Should().BeFalse();
        dto.Sections.Should().HaveCount(1);
        dto.Sections[0].Code.Should().Be("identity");
        dto.Sections[0].Status.Should().Be("Ready");
        dto.Sections[0].Items.Should().HaveCount(1);
        var item = dto.Sections[0].Items[0];
        item.Code.Should().Be("identity.taxId");
        item.Status.Should().Be("Ready");
        item.Severity.Should().Be("Blocking");
        item.BlockingArea.Should().Be("Sales");
        item.ActionTarget.Should().Be("CompanyProfile");
    }

    [Fact]
    public async Task Handle_resuelve_tenant_y_company_del_contexto_autenticado_nunca_del_request()
    {
        var f = new Fixture();
        f.Resolver
            .Setup(r => r.GetAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new CompanyOperationalReadinessResult(
                    ReadinessStatus.Ready,
                    true,
                    true,
                    true,
                    true,
                    new List<ReadinessSection>()
                )
            );

        await f.BuildHandler().Handle(new GetCompanyOperationalReadinessQuery(), default);

        f.Resolver.Verify(
            r => r.GetAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    /// <summary>
    /// El DTO no debe exponer entidades de dominio (Company/Branch/etc.) ni tipos EF — solo
    /// primitivos, string, bool y colecciones de records propios del DTO.
    /// </summary>
    [Fact]
    public void Dto_no_expone_tipos_de_entidad_de_dominio()
    {
        var allowedNamespacePrefix = "ERP.Application.Modules.Companies.DTOs";

        void AssertNoEntityLeak(Type type, HashSet<Type> visited)
        {
            if (!visited.Add(type))
                return;

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var propType = UnwrapCollection(prop.PropertyType);

                if (propType.Namespace is null)
                    continue;
                if (propType.Namespace.StartsWith("System", StringComparison.Ordinal))
                    continue;

                propType.Namespace.Should().NotContain(".Entities", $"la propiedad {type.Name}.{prop.Name} no debe exponer una entidad de dominio");
                propType.Namespace.Should().NotStartWith("ERP.Domain.Branches");

                if (propType.Namespace.StartsWith(allowedNamespacePrefix, StringComparison.Ordinal))
                    AssertNoEntityLeak(propType, visited);
            }
        }

        static Type UnwrapCollection(Type t)
        {
            if (t.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(t))
                return t.GetGenericArguments()[0];
            return t;
        }

        AssertNoEntityLeak(typeof(CompanyOperationalReadinessDto), new HashSet<Type>());
    }
}
