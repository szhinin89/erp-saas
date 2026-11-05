using ERP.Application.Common;
using ERP.Application.MasterData.DTOs;
using ERP.Application.MasterData.UseCases.UpdateRoleConfig;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.MasterData;

/// <summary>
/// BUG-CUSTOMERS-CLASSIFICATION-01 — fija el contrato vigente de PATCH customer-config:
/// la configuración enviada REEMPLAZA la persistida por completo (null/vacío = limpiar). El modal
/// "Clasificación" depende de este contrato para precargar y reenviar los valores no modificados.
/// Sin cambio de semántica en esta corrección; datos ficticios, sin base de datos.
/// </summary>
public sealed class UpdateCustomerRoleConfigHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BpId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static BusinessPartnerRole ExistingCustomerRole() =>
        BusinessPartnerRole.Create(
            TenantId,
            BpId,
            RoleType.Customer,
            UserId,
            customerConfig: CustomerRoleConfig.Create(
                "WHOLESALE",
                "SMB",
                "Norte",
                "AA",
                "GOLD",
                "PDF",
                "NACIONAL"
            )
        );

    private static (Mock<IBusinessPartnerRoleRepository> Repo, UpdateCustomerRoleConfigHandler Handler) Sut(
        BusinessPartnerRole? role
    )
    {
        var repo = new Mock<IBusinessPartnerRoleRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        var ctx = new Mock<IOperationalContext>();
        ctx.Setup(c => c.TenantId).Returns(TenantId);
        ctx.Setup(c => c.UserId).Returns(UserId);
        return (repo, new UpdateCustomerRoleConfigHandler(repo.Object, ctx.Object));
    }

    [Fact]
    public async Task Reenviar_todos_los_valores_con_un_cambio_conserva_los_demas()
    {
        var role = ExistingCustomerRole();
        var (repo, handler) = Sut(role);

        var result = await handler.Handle(
            new UpdateCustomerRoleConfigCommand(
                BpId,
                role.Id,
                new CustomerRoleConfigDto("WHOLESALE", "ENTERPRISE", "Norte", "AA", "GOLD", "PDF", "NACIONAL")
            ),
            default
        );

        result.IsSuccess.Should().BeTrue();
        role.CustomerConfig.Should()
            .Be(CustomerRoleConfig.Create("WHOLESALE", "ENTERPRISE", "Norte", "AA", "GOLD", "PDF", "NACIONAL"));
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Los_campos_omitidos_o_nulos_se_limpian_porque_el_contrato_es_de_reemplazo()
    {
        var role = ExistingCustomerRole();
        var (_, handler) = Sut(role);

        var result = await handler.Handle(
            new UpdateCustomerRoleConfigCommand(
                BpId,
                role.Id,
                new CustomerRoleConfigDto(null, "SMB", "  ", "AA", "GOLD", "PDF", "NACIONAL")
            ),
            default
        );

        result.IsSuccess.Should().BeTrue();
        role.CustomerConfig!.CustomerCategory.Should().BeNull("null limpia deliberadamente el campo");
        role.CustomerConfig.SalesZone.Should().BeNull("texto vacío se normaliza a null");
        role.CustomerConfig.CustomerSegment.Should().Be("SMB");
        role.CustomerConfig.CustomerClassification.Should().Be("NACIONAL");
    }

    [Fact]
    public async Task Rol_de_otro_socio_no_se_guarda_ni_altera_la_configuracion()
    {
        var role = ExistingCustomerRole();
        var before = role.CustomerConfig;
        var (repo, handler) = Sut(role);

        var result = await handler.Handle(
            new UpdateCustomerRoleConfigCommand(
                Guid.NewGuid(),
                role.Id,
                new CustomerRoleConfigDto(null, null, null, null, null, null, null)
            ),
            default
        );

        result.IsSuccess.Should().BeFalse();
        role.CustomerConfig.Should().Be(before);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
