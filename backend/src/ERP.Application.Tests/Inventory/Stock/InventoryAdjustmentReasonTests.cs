using ERP.Application.Common;
using ERP.Application.Modules.Inventory.AdjustmentReasons.UseCases.CreateInventoryAdjustmentReason;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Inventory.Stock;

/// <summary>INVENTORY-ADJUSTMENTS-02 — catálogo administrable de motivos de ajuste.</summary>
public sealed class InventoryAdjustmentReasonTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
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
            null,
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
    public void AllowsMovementType_respeta_restriccion_de_tipo()
    {
        var ingresoOnly = InventoryAdjustmentReason.Create(
            TenantId,
            null,
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
            null,
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
        act.Should().Throw<InvalidOperationException>();

        reason.Enable(UserId);
        reason.IsActive.Should().BeTrue();
    }
}
