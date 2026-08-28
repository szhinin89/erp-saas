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

    // ══════════════════════════════════════════════════════════════════════
    // SUPPLIER-PAYMENTS-REVERSE-16 — ReversePaymentToInstallment (inverso de
    // RegisterPaymentToInstallment)
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public void ReversePaymentToInstallment_de_pago_total_vuelve_la_cuota_de_Paid_a_Pending()
    {
        var payable = CreatePayable();
        var installment = payable.AddInstallment(1, new DateOnly(2026, 9, 26), 300m);
        payable.RegisterPaymentToInstallment(installment.Id, 300m, UserId);
        payable.Status.Should().Be(AccountsPayableStatus.Paid);

        payable.ReversePaymentToInstallment(installment.Id, 300m, UserId);

        installment.PaidAmount.Should().Be(0m);
        installment.OutstandingAmount.Should().Be(300m);
        installment.Status.Should().Be(AccountsPayableStatus.Pending);
        payable.Status.Should().Be(AccountsPayableStatus.Pending);
    }

    [Fact]
    public void ReversePaymentToInstallment_de_pago_parcial_deja_la_cuota_en_PartiallyPaid()
    {
        var payable = CreatePayable();
        var installment = payable.AddInstallment(1, new DateOnly(2026, 9, 26), 300m);
        payable.RegisterPaymentToInstallment(installment.Id, 300m, UserId);

        payable.ReversePaymentToInstallment(installment.Id, 100m, UserId);

        installment.PaidAmount.Should().Be(200m);
        installment.OutstandingAmount.Should().Be(100m);
        installment.Status.Should().Be(AccountsPayableStatus.PartiallyPaid);
        payable.Status.Should().Be(AccountsPayableStatus.PartiallyPaid);
    }

    [Fact]
    public void ReversePaymentToInstallment_rechaza_monto_mayor_al_pagado_nunca_deja_PaidAmount_negativo()
    {
        var payable = CreatePayable();
        var installment = payable.AddInstallment(1, new DateOnly(2026, 9, 26), 300m);
        payable.RegisterPaymentToInstallment(installment.Id, 100m, UserId);

        var act = () => payable.ReversePaymentToInstallment(installment.Id, 150m, UserId);

        act.Should().Throw<InvalidOperationException>();
        installment.PaidAmount.Should().Be(100m, "el intento rechazado no debe mutar el saldo");
    }

    [Fact]
    public void ReversePaymentToInstallment_rechaza_cuota_que_no_pertenece_a_esta_CxP()
    {
        var payable = CreatePayable();
        payable.AddInstallment(1, new DateOnly(2026, 9, 26), 300m);

        var act = () => payable.ReversePaymentToInstallment(Guid.NewGuid(), 100m, UserId);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ReversePaymentToInstallment_rechaza_monto_menor_o_igual_a_cero()
    {
        var payable = CreatePayable();
        var installment = payable.AddInstallment(1, new DateOnly(2026, 9, 26), 300m);
        payable.RegisterPaymentToInstallment(installment.Id, 300m, UserId);

        var act = () => payable.ReversePaymentToInstallment(installment.Id, 0m, UserId);

        act.Should().Throw<ArgumentException>();
    }
}
