using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Payables;

public sealed class AccountsPayableTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid OriginId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static AccountsPayable CreatePayable(
        AccountsPayableOriginType originType = AccountsPayableOriginType.ExpenseDocument,
        Guid? originId = null
    ) =>
        AccountsPayable.CreateFromOrigin(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            originType,
            originId ?? OriginId,
            "01",
            "001-001-000000001",
            new DateOnly(2026, 8, 27),
            new DateOnly(2026, 8, 27),
            UserId
        );

    [Fact]
    public void Crear_CxP_valida_con_una_cuota_calcula_totales_correctamente()
    {
        var payable = CreatePayable();

        payable.AddInstallment(1, new DateOnly(2026, 9, 26), 115m);

        payable.TotalAmount.Should().Be(115m);
        payable.PaidAmount.Should().Be(0m);
        payable.OutstandingAmount.Should().Be(115m);
        payable.Installments.Should().ContainSingle();
    }

    [Fact]
    public void Estado_inicial_de_la_CxP_es_Pending()
    {
        var payable = CreatePayable();
        payable.AddInstallment(1, new DateOnly(2026, 9, 26), 100m);

        payable.Status.Should().Be(AccountsPayableStatus.Pending);
    }

    [Fact]
    public void Estado_inicial_de_la_cuota_es_Pending()
    {
        var payable = CreatePayable();

        var installment = payable.AddInstallment(1, new DateOnly(2026, 9, 26), 100m);

        installment.Status.Should().Be(AccountsPayableStatus.Pending);
        installment.PaidAmount.Should().Be(0m);
    }

    [Fact]
    public void OutstandingAmount_es_igual_a_TotalAmount_al_crear()
    {
        var payable = CreatePayable();
        payable.AddInstallment(1, new DateOnly(2026, 9, 26), 250m);

        payable.OutstandingAmount.Should().Be(payable.TotalAmount);
    }

    [Fact]
    public void Bloquea_cuota_con_Amount_cero()
    {
        var payable = CreatePayable();

        var act = () => payable.AddInstallment(1, new DateOnly(2026, 9, 26), 0m);

        act.Should().Throw<ArgumentException>().WithMessage("*mayor a cero*");
    }

    [Fact]
    public void Bloquea_cuota_con_Amount_negativo()
    {
        var payable = CreatePayable();

        var act = () => payable.AddInstallment(1, new DateOnly(2026, 9, 26), -10m);

        act.Should().Throw<ArgumentException>().WithMessage("*mayor a cero*");
    }

    [Fact]
    public void Bloquea_dos_cuotas_con_el_mismo_numero()
    {
        var payable = CreatePayable();
        payable.AddInstallment(1, new DateOnly(2026, 9, 26), 50m);

        var act = () => payable.AddInstallment(1, new DateOnly(2026, 10, 26), 60m);

        act.Should().Throw<InvalidOperationException>().WithMessage("*cuota*1*");
    }

    [Fact]
    public void Bloquea_CxP_sin_proveedor()
    {
        var act = () =>
            AccountsPayable.CreateFromOrigin(
                TenantId, CompanyId, BranchId, Guid.Empty,
                AccountsPayableOriginType.ExpenseDocument, OriginId,
                "01", "001-001-000000001",
                new DateOnly(2026, 8, 27), new DateOnly(2026, 8, 27), UserId
            );

        act.Should().Throw<ArgumentException>().WithMessage("*proveedor*");
    }

    [Fact]
    public void Bloquea_CxP_sin_documento_de_origen()
    {
        var act = () =>
            AccountsPayable.CreateFromOrigin(
                TenantId, CompanyId, BranchId, SupplierId,
                AccountsPayableOriginType.ExpenseDocument, Guid.Empty,
                "01", "001-001-000000001",
                new DateOnly(2026, 8, 27), new DateOnly(2026, 8, 27), UserId
            );

        act.Should().Throw<ArgumentException>().WithMessage("*origen*");
    }

    [Fact]
    public void AccountsPayable_no_referencia_conceptos_de_inventario_kardex_o_pvp()
    {
        var forbiddenPropertyNames = new[]
        {
            "ItemId",
            "WarehouseId",
            "PackagingLevelId",
            "KardexId",
            "PurchaseInvoiceDetailId",
            "Pvp",
            "AverageCost",
        };

        var propertyNames = typeof(AccountsPayable)
            .GetProperties()
            .Select(p => p.Name)
            .Concat(typeof(AccountsPayableInstallment).GetProperties().Select(p => p.Name))
            .ToHashSet();

        propertyNames.Should().NotContain(forbiddenPropertyNames);
    }
}
