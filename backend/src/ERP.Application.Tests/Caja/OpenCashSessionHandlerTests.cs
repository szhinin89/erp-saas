using ERP.Application.Common;
using ERP.Application.Modules.Caja.UseCases;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Caja;

/// <summary>
/// Fase 2 (ADR — Rediseño del módulo de Caja) — OpenCashSessionHandler resuelve CashRegister,
/// deriva BranchId/CompanyId/EmissionPointId exclusivamente server-side (nunca del cliente) y
/// aplica las 6 reglas de apertura: caja existe, activa, misma sucursal, con punto de emisión,
/// sin sesión abierta por caja, sin sesión abierta por usuario.
/// </summary>
public sealed class OpenCashSessionHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid EstablishmentId = Guid.NewGuid();

    private static EmissionPoint CreateEmissionPoint(Guid id) =>
        EmissionPoint.Create(TenantId, CompanyId, EstablishmentId, "001", null, EmissionType.Electronic, true, UserId);

    private static CashRegister CreateRegister(Guid branchId, Guid? emissionPointId) =>
        CashRegister.Create(TenantId, CompanyId, branchId, "CAJA-01", "Caja Principal", UserId, emissionPointId);

    private sealed class Fixture
    {
        public Mock<ICashSessionRepository> Repo { get; } = new();
        public Mock<ICashRegisterRepository> CashRegisterRepo { get; } = new();
        public Mock<IEmissionPointRepository> EmissionPointRepo { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentBranch> Branch { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();

        public Fixture()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            User.Setup(u => u.UserId).Returns(UserId);
            Repo.Setup(r => r.GetOpenByUserAsync(TenantId, UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((CashSession?)null);
            Repo.Setup(r => r.GetOpenByCashRegisterAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CashSession?)null);
        }

        public OpenCashSessionHandler BuildHandler(Guid branchId)
        {
            Branch.Setup(b => b.BranchId).Returns(branchId);
            return new OpenCashSessionHandler(
                Repo.Object, CashRegisterRepo.Object, EmissionPointRepo.Object,
                Tenant.Object, Branch.Object, User.Object);
        }
    }

    [Fact]
    public async Task Abrir_caja_con_registro_valido_persiste_BranchId_y_EmissionPointId_desde_CashRegister()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var emissionPointId = Guid.NewGuid();
        var register = CreateRegister(branchId, emissionPointId);
        var emissionPoint = CreateEmissionPoint(emissionPointId);

        f.CashRegisterRepo.Setup(r => r.GetByIdAsync(TenantId, register.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(register);
        f.EmissionPointRepo.Setup(r => r.GetByIdAsync(emissionPointId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emissionPoint);

        CashSession? captured = null;
        f.Repo.Setup(r => r.AddAsync(It.IsAny<CashSession>(), It.IsAny<CancellationToken>()))
            .Callback<CashSession, CancellationToken>((s, _) => captured = s)
            .Returns(Task.CompletedTask);

        var handler = f.BuildHandler(branchId);
        var result = await handler.Handle(new OpenCashSessionCommand(register.Id, 50m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.BranchId.Should().Be(branchId);
        captured.CashRegisterId.Should().Be(register.Id);
        captured.EmissionPointId.Should().Be(emissionPointId);
        captured.CashRegisterCodeSnapshot.Should().Be(register.Code);
        captured.CashRegisterNameSnapshot.Should().Be(register.Name);
        captured.EmissionPointCodeSnapshot.Should().Be(emissionPoint.Code);
    }

    [Fact]
    public async Task Caja_inexistente_retorna_NotFound()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var missingId = Guid.NewGuid();
        f.CashRegisterRepo.Setup(r => r.GetByIdAsync(TenantId, missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CashRegister?)null);

        var handler = f.BuildHandler(branchId);
        var result = await handler.Handle(new OpenCashSessionCommand(missingId, 50m), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Caja_inactiva_retorna_error_de_validacion()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var register = CreateRegister(branchId, Guid.NewGuid());
        register.Disable(UserId);
        f.CashRegisterRepo.Setup(r => r.GetByIdAsync(TenantId, register.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(register);

        var handler = f.BuildHandler(branchId);
        var result = await handler.Handle(new OpenCashSessionCommand(register.Id, 50m), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Caja_sin_punto_de_emision_retorna_error_de_validacion()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var register = CreateRegister(branchId, emissionPointId: null);
        f.CashRegisterRepo.Setup(r => r.GetByIdAsync(TenantId, register.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(register);

        var handler = f.BuildHandler(branchId);
        var result = await handler.Handle(new OpenCashSessionCommand(register.Id, 50m), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Caja_de_otra_sucursal_retorna_error_de_validacion()
    {
        var f = new Fixture();
        var registerBranchId = Guid.NewGuid();
        var activeBranchId = Guid.NewGuid();
        var register = CreateRegister(registerBranchId, Guid.NewGuid());
        f.CashRegisterRepo.Setup(r => r.GetByIdAsync(TenantId, register.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(register);

        var handler = f.BuildHandler(activeBranchId);
        var result = await handler.Handle(new OpenCashSessionCommand(register.Id, 50m), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Usuario_con_sesion_abierta_retorna_error_de_validacion()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var register = CreateRegister(branchId, Guid.NewGuid());
        var existing = CashSession.Open(
            TenantId, CompanyId, branchId, UserId,
            Guid.NewGuid(), "CAJA-00", "Otra Caja",
            Guid.NewGuid(), "001", 10m, UserId);
        f.Repo.Setup(r => r.GetOpenByUserAsync(TenantId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var handler = f.BuildHandler(branchId);
        var result = await handler.Handle(new OpenCashSessionCommand(register.Id, 50m), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        f.CashRegisterRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Caja_con_sesion_abierta_retorna_error_de_validacion()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var register = CreateRegister(branchId, Guid.NewGuid());
        var existing = CashSession.Open(
            TenantId, CompanyId, branchId, Guid.NewGuid(),
            register.Id, register.Code, register.Name,
            Guid.NewGuid(), "001", 10m, UserId);

        f.CashRegisterRepo.Setup(r => r.GetByIdAsync(TenantId, register.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(register);
        f.Repo.Setup(r => r.GetOpenByCashRegisterAsync(TenantId, register.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var handler = f.BuildHandler(branchId);
        var result = await handler.Handle(new OpenCashSessionCommand(register.Id, 50m), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task EmissionPointId_proviene_del_CashRegister_y_no_del_cliente()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var registerEmissionPointId = Guid.NewGuid();
        var register = CreateRegister(branchId, registerEmissionPointId);
        var emissionPoint = CreateEmissionPoint(registerEmissionPointId);

        f.CashRegisterRepo.Setup(r => r.GetByIdAsync(TenantId, register.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(register);
        f.EmissionPointRepo.Setup(r => r.GetByIdAsync(registerEmissionPointId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emissionPoint);

        CashSession? captured = null;
        f.Repo.Setup(r => r.AddAsync(It.IsAny<CashSession>(), It.IsAny<CancellationToken>()))
            .Callback<CashSession, CancellationToken>((s, _) => captured = s)
            .Returns(Task.CompletedTask);

        // El command (OpenCashSessionCommand) no expone ninguna propiedad EmissionPointId —
        // el único dato de entrada del cliente es CashRegisterId + OpeningAmount + Notes.
        typeof(OpenCashSessionCommand).GetProperty("EmissionPointId").Should().BeNull(
            "el cliente nunca debe poder enviar el punto de emisión — se resuelve desde CashRegister");

        var handler = f.BuildHandler(branchId);
        await handler.Handle(new OpenCashSessionCommand(register.Id, 50m), CancellationToken.None);

        captured!.EmissionPointId.Should().Be(registerEmissionPointId);
    }
}
