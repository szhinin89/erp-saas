using ERP.Domain.Modules.Purchases.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Purchases;

/// <summary>
/// Fase 3 (Branch Ownership) — PurchaseInvoice.BranchId se asigna exclusivamente en
/// CreateDraft, nunca se recibe desde el cliente, y es inmutable tras la creación.
/// </summary>
public sealed class PurchaseInvoiceBranchOwnershipTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static PurchaseInvoice CreateDraft(Guid branchId) =>
        PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            branchId,
            Guid.NewGuid(),
            "Proveedor Test",
            "1234567890001",
            "01",
            $"001-001-{Random.Shared.Next(100000000, 999999999)}",
            DateOnly.FromDateTime(DateTime.UtcNow),
            UserId,
            Guid.NewGuid(),
            "Contado",
            1,
            0
        );

    [Fact]
    public void CreateDraft_con_sucursal_valida_persiste_BranchId()
    {
        var branchId = Guid.NewGuid();

        var inv = CreateDraft(branchId);

        inv.BranchId.Should().Be(branchId);
    }

    [Fact]
    public void CreateDraft_con_BranchId_vacio_lanza_ArgumentException()
    {
        var act = () => CreateDraft(Guid.Empty);

        act.Should().Throw<ArgumentException>().WithParameterName("branchId");
    }

    [Fact]
    public void BranchId_no_expone_setter_publico_ni_metodo_ChangeBranch()
    {
        var property = typeof(PurchaseInvoice).GetProperty(nameof(PurchaseInvoice.BranchId))!;
        property.SetMethod.Should().NotBeNull();
        property.SetMethod!.IsPublic.Should().BeFalse("BranchId solo se asigna en CreateDraft");

        typeof(PurchaseInvoice)
            .GetMethods()
            .Any(m => m.Name is "ChangeBranch" or "SetBranch" or "UpdateBranch")
            .Should()
            .BeFalse("no debe existir ningún método para mutar la sucursal tras la creación");
    }

    [Fact]
    public void Dos_compras_creadas_con_sucursales_distintas_mantienen_su_BranchId_independiente()
    {
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();

        var invA = CreateDraft(branchA);
        var invB = CreateDraft(branchB);

        invA.BranchId.Should().Be(branchA);
        invB.BranchId.Should().Be(branchB);
        invA.BranchId.Should().NotBe(invB.BranchId);
    }
}
