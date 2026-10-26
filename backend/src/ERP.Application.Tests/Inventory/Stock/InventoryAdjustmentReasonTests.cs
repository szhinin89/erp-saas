using ERP.Application.Common;
using ERP.Application.Modules.Inventory.AdjustmentReasons.UseCases.CreateInventoryAdjustmentReason;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Inventory.Stock;

/// <summary>INVENTORY-ADJUSTMENTS-02 — catálogo administrable de motivos de ajuste.</summary>
public sealed class InventoryAdjustmentReasonTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Crear_motivo_con_codigo_duplicado_es_rechazado()
    {
        var repo = new Mock<IInventoryAdjustmentReasonRepository>();
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);

        var existing = InventoryAdjustmentReason.Create(
            TenantId,
            CompanyId,
            "MERMA",
            "Merma",
            InventoryAdjustmentReason.Ambos,
            false,
            1,
            UserId
        );
        repo.Setup(r => r.GetByCodeAsync(TenantId, "MERMA", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var handler = new CreateInventoryAdjustmentReasonCommandHandler(
            repo.Object,
            tenant.Object,
            Company().Object,
            user.Object
        );

        var result = await handler.Handle(
            new CreateInventoryAdjustmentReasonCommand(
                null,
                "MERMA",
                "Merma duplicada",
                InventoryAdjustmentReason.Ambos,
                false,
                1
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        repo.Verify(
            r => r.AddAsync(It.IsAny<InventoryAdjustmentReason>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Crear_motivo_asigna_la_empresa_del_contexto()
    {
        var repo = new Mock<IInventoryAdjustmentReasonRepository>();
        InventoryAdjustmentReason? added = null;
        repo.Setup(r => r.AddAsync(It.IsAny<InventoryAdjustmentReason>(), It.IsAny<CancellationToken>()))
            .Callback<InventoryAdjustmentReason, CancellationToken>((r, _) => added = r);
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);

        var handler = new CreateInventoryAdjustmentReasonCommandHandler(
            repo.Object,
            tenant.Object,
            Company().Object,
            user.Object
        );

        var result = await handler.Handle(
            new CreateInventoryAdjustmentReasonCommand(
                null,
                "MERMA",
                "Merma",
                InventoryAdjustmentReason.Ambos,
                false,
                1
            ),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        added!.TenantId.Should().Be(TenantId);
        added.CompanyId.Should().Be(CompanyId);
    }

    [Fact]
    public void Crear_motivo_sin_empresa_es_rechazado()
    {
        var act = () =>
            InventoryAdjustmentReason.Create(
                TenantId,
                Guid.Empty,
                "MERMA",
                "Merma",
                InventoryAdjustmentReason.Ambos,
                false,
                1,
                UserId
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AllowsMovementType_respeta_restriccion_de_tipo()
    {
        var ingresoOnly = InventoryAdjustmentReason.Create(
            TenantId,
            CompanyId,
            "ING",
            "Solo ingreso",
            InventoryAdjustmentReason.Ingreso,
            false,
            1,
            UserId
        );

        ingresoOnly.AllowsMovementType(InventoryAdjustmentReason.Ingreso).Should().BeTrue();
        ingresoOnly.AllowsMovementType(InventoryAdjustmentReason.Egreso).Should().BeFalse();
    }

    [Fact]
    public void Disable_y_Enable_son_idempotentes_en_su_propia_direccion()
    {
        var reason = InventoryAdjustmentReason.Create(
            TenantId,
            CompanyId,
            "MERMA",
            "Merma",
            InventoryAdjustmentReason.Ambos,
            false,
            1,
            UserId
        );

        reason.Disable(UserId);
        reason.IsActive.Should().BeFalse();

        var act = () => reason.Disable(UserId);
        act.Should().Throw<DomainRuleViolationException>();

        reason.Enable(UserId);
        reason.IsActive.Should().BeTrue();
    }

    private static Mock<ICurrentCompany> Company()
    {
        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        return company;
    }
}
