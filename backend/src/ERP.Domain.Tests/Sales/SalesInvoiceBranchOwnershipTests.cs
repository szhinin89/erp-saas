using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.ValueObjects;
using FluentAssertions;

namespace ERP.Domain.Tests.Sales;

/// <summary>
/// Fase 3 (Branch Ownership) — SalesInvoice.BranchId se asigna exclusivamente en CreateDraft,
/// nunca se recibe desde el cliente (el handler lo pasa desde ICurrentBranch) y es inmutable
/// tras la creación (sin setter público, sin ChangeBranch).
/// </summary>
public sealed class SalesInvoiceBranchOwnershipTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static CustomerSnapshot Customer() =>
        CustomerSnapshot.Create("Cliente Test", "0999999999", "05");

    private static PaymentTermSnapshot PaymentTerm() =>
        PaymentTermSnapshot.Create(Guid.NewGuid(), "Contado", 1, 0);

    private static SalesInvoice CreateDraft(Guid branchId) =>
        SalesInvoice.CreateDraft(
            TenantId, CompanyId, branchId, Guid.NewGuid(), Customer(),
            "DRAFT-TEST", DateOnly.FromDateTime(DateTime.UtcNow), UserId, PaymentTerm(),
            cashSessionId: Guid.NewGuid());

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
        var property = typeof(SalesInvoice).GetProperty(nameof(SalesInvoice.BranchId))!;
        property.SetMethod.Should().NotBeNull();
        property.SetMethod!.IsPublic.Should().BeFalse("BranchId solo se asigna en CreateDraft");

        typeof(SalesInvoice).GetMethods()
            .Any(m => m.Name is "ChangeBranch" or "SetBranch" or "UpdateBranch")
            .Should().BeFalse("no debe existir ningún método para mutar la sucursal tras la creación");
    }

    [Fact]
    public void CreateDraft_con_CashSessionId_vacio_lanza_ArgumentException()
    {
        var act = () => SalesInvoice.CreateDraft(
            TenantId, CompanyId, Guid.NewGuid(), Guid.NewGuid(), Customer(),
            "DRAFT-TEST", DateOnly.FromDateTime(DateTime.UtcNow), UserId, PaymentTerm(),
            cashSessionId: Guid.Empty);

        act.Should().Throw<ArgumentException>().WithParameterName("cashSessionId");
    }

    [Fact]
    public void CashSessionId_no_expone_setter_publico_ni_metodo_de_cambio()
    {
        var property = typeof(SalesInvoice).GetProperty(nameof(SalesInvoice.CashSessionId))!;
        property.SetMethod.Should().NotBeNull();
        property.SetMethod!.IsPublic.Should().BeFalse("CashSessionId solo se asigna en CreateDraft");

        typeof(SalesInvoice).GetMethods()
            .Any(m => m.Name is "ChangeCashSession" or "SetCashSession" or "UpdateCashSession")
            .Should().BeFalse("no debe existir ningún método para mutar la caja tras la creación");
    }

    [Fact]
    public void Dos_facturas_creadas_con_sucursales_distintas_mantienen_su_BranchId_independiente()
    {
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();

        var invA = CreateDraft(branchA);
        var invB = CreateDraft(branchB);

        invA.BranchId.Should().Be(branchA);
        invB.BranchId.Should().Be(branchB);
        invA.BranchId.Should().NotBe(invB.BranchId);

        // Cambiar de contexto (branchB) después de creada invA nunca la afecta — no hay
        // ninguna operación en el ciclo de vida del documento que lea el contexto de sesión
        // activo para recalcular BranchId.
        invA.UpdateDraft(Guid.NewGuid(), Customer(), DateOnly.FromDateTime(DateTime.UtcNow), UserId);
        invA.BranchId.Should().Be(branchA);
    }
}
