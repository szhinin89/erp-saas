using ERP.Domain.Modules.Caja.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Caja;

/// <summary>
/// Fase 3 (Branch Ownership) — CashSession.BranchId se asigna exclusivamente en Open, nunca
/// se recibe desde el cliente, y es inmutable tras la apertura.
/// </summary>
public sealed class CashSessionBranchOwnershipTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static CashSession Open(Guid branchId) =>
        CashSession.Open(
            TenantId,
            CompanyId,
            branchId,
            UserId,
            Guid.NewGuid(),
            "CAJA-01",
            "Caja Principal",
            Guid.NewGuid(),
            "001",
            50m,
            UserId
        );

    [Fact]
    public void Open_con_sucursal_valida_persiste_BranchId()
    {
        var branchId = Guid.NewGuid();

        var session = Open(branchId);

        session.BranchId.Should().Be(branchId);
    }

    [Fact]
    public void Open_con_BranchId_vacio_lanza_ArgumentException()
    {
        var act = () => Open(Guid.Empty);

        act.Should().Throw<ArgumentException>().WithParameterName("branchId");
    }

    [Fact]
    public void BranchId_no_expone_setter_publico_ni_metodo_ChangeBranch()
    {
        var property = typeof(CashSession).GetProperty(nameof(CashSession.BranchId))!;
        property.SetMethod.Should().NotBeNull();
        property.SetMethod!.IsPublic.Should().BeFalse("BranchId solo se asigna en Open");

        typeof(CashSession)
            .GetMethods()
            .Any(m => m.Name is "ChangeBranch" or "SetBranch" or "UpdateBranch")
            .Should()
            .BeFalse("no debe existir ningún método para mutar la sucursal tras la apertura");
    }

    [Fact]
    public void Dos_cajas_abiertas_con_sucursales_distintas_mantienen_su_BranchId_independiente()
    {
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();

        var sessionA = Open(branchA);
        var sessionB = Open(branchB);

        sessionA.BranchId.Should().Be(branchA);
        sessionB.BranchId.Should().Be(branchB);
        sessionA.BranchId.Should().NotBe(sessionB.BranchId);
    }
}
