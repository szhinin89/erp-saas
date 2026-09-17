using ERP.Domain.Modules.Sales.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Sales;

/// <summary>SALES-TRANSFER-BANK-ACCOUNT-01: <see cref="PaymentTransferDetail"/> ahora exige CompanyBankAccountId + comprobante + fecha — BankName libre queda solo como legado.</summary>
public sealed class PaymentTransferDetailTests
{
    private static readonly Guid PaymentId = Guid.NewGuid();
    private static readonly Guid CompanyBankAccountId = Guid.NewGuid();

    [Fact]
    public void Create_con_datos_validos_asigna_todos_los_campos()
    {
        var detail = PaymentTransferDetail.Create(
            PaymentId,
            CompanyBankAccountId,
            "TRX-001",
            new DateOnly(2026, 9, 1)
        );

        detail.PaymentId.Should().Be(PaymentId);
        detail.CompanyBankAccountId.Should().Be(CompanyBankAccountId);
        detail.ReceiptNumber.Should().Be("TRX-001");
        detail.TransferDate.Should().Be(new DateOnly(2026, 9, 1));
        detail.BankName.Should().BeNull();
    }

    [Fact]
    public void Create_recorta_espacios_del_comprobante()
    {
        var detail = PaymentTransferDetail.Create(
            PaymentId,
            CompanyBankAccountId,
            "  TRX-001  ",
            new DateOnly(2026, 9, 1)
        );

        detail.ReceiptNumber.Should().Be("TRX-001");
    }

    [Fact]
    public void Create_sin_cuenta_bancaria_lanza()
    {
        var act = () =>
            PaymentTransferDetail.Create(PaymentId, Guid.Empty, "TRX-001", new DateOnly(2026, 9, 1));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_sin_comprobante_lanza()
    {
        var act = () =>
            PaymentTransferDetail.Create(
                PaymentId,
                CompanyBankAccountId,
                "   ",
                new DateOnly(2026, 9, 1)
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_con_comprobante_demasiado_largo_lanza()
    {
        var act = () =>
            PaymentTransferDetail.Create(
                PaymentId,
                CompanyBankAccountId,
                new string('A', PaymentTransferDetail.ReceiptMaxLen + 1),
                new DateOnly(2026, 9, 1)
            );

        act.Should().Throw<ArgumentException>();
    }
}
