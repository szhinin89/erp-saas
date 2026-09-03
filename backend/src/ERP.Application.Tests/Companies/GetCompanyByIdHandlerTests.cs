using ERP.Application.Common;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.Companies.UseCases.GetCompanyById;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Companies;

/// <summary>
/// MEDIO cluster (auditoría multi-tenant) — GetCompanyByIdHandler cierra el acceso en dos capas:
/// (1) ICompanyAccessGuard.RequireMembershipAsync (sin membresía → Failure) y (2) un chequeo
/// defensivo adicional dentro del handler que compara company.TenantId contra el TenantId
/// devuelto por el guard, por si el guard alguna vez resolviera membresía sin garantizar
/// pertenencia al mismo tenant. Este test prueba ambas capas por separado.
/// </summary>
public sealed class GetCompanyByIdHandlerTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly Mock<ICompanyAccessGuard> _accessGuard = new();
    private readonly Mock<ICompanyRepository> _companies = new();

    private GetCompanyByIdHandler BuildHandler() => new(_accessGuard.Object, _companies.Object);

    [Fact]
    public async Task Sin_membresia_en_la_empresa_solicitada_el_guard_rechaza_y_el_repo_nunca_se_consulta()
    {
        var otherCompanyId = Guid.NewGuid();
        _accessGuard
            .Setup(g =>
                g.RequireMembershipAsync(otherCompanyId, false, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(Result<CompanyAccessContext>.Forbidden("No tiene acceso a esta empresa."));

        var handler = BuildHandler();
        var result = await handler.Handle(
            new GetCompanyByIdQuery(otherCompanyId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        _companies.Verify(
            c => c.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Empresa_devuelta_por_el_repo_pertenece_a_otro_tenant_que_el_del_guard_falla_igual()
    {
        // Defensa en profundidad: aunque el guard (hipotéticamente mal configurado o con un bug)
        // devolviera éxito, el handler vuelve a comparar TenantId contra la entidad real leída del
        // repositorio antes de exponerla — nunca confía ciegamente en el resultado del guard.
        var companyId = Guid.NewGuid();
        var companyOfTenantB = Company.CreateManaged(
            TenantB,
            "1790012345001",
            "Empresa de Tenant B",
            createdBy: UserId
        );
        companyOfTenantB.Id = companyId;

        _accessGuard
            .Setup(g => g.RequireMembershipAsync(companyId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<CompanyAccessContext>.Success(
                    new CompanyAccessContext(UserId, TenantA, companyId, "Admin", true, true)
                )
            );
        _companies
            .Setup(c => c.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(companyOfTenantB);

        var handler = BuildHandler();
        var result = await handler.Handle(
            new GetCompanyByIdQuery(companyId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no encontrada");
    }

    [Fact]
    public async Task Empresa_del_mismo_tenant_del_guard_se_devuelve_correctamente()
    {
        var companyId = Guid.NewGuid();
        var companyOfTenantA = Company.CreateManaged(
            TenantA,
            "1790012345001",
            "Empresa de Tenant A",
            createdBy: UserId
        );
        companyOfTenantA.Id = companyId;

        _accessGuard
            .Setup(g => g.RequireMembershipAsync(companyId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<CompanyAccessContext>.Success(
                    new CompanyAccessContext(UserId, TenantA, companyId, "Admin", true, true)
                )
            );
        _companies
            .Setup(c => c.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(companyOfTenantA);

        var handler = BuildHandler();
        var result = await handler.Handle(
            new GetCompanyByIdQuery(companyId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(companyId);
    }
}
