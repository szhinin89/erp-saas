using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Finance;

public sealed class SupplierCreditRefundTransactionTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid SupplierCreditId = Guid.NewGuid();
    private static readonly Guid SupplierCreditMovementId = Guid.NewGuid();
    private static readonly Guid FinancialDestinationId = Guid.NewGuid();
    private static readonly Guid AccountingAccountId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static SupplierCreditRefundTransaction CreateReceived(
        decimal amount = 150m,
        Guid? clientRequestId = null,
        string payloadHash = "hash-received-default"
    ) =>
        SupplierCreditRefundTransaction.CreateReceived(
            TenantId,
            CompanyId,
            SupplierId,
            SupplierCreditId,
            SupplierCreditMovementId,
            FinancialDestinationId,
            AccountingAccountId,
            accountingAccountCodeSnapshot: "1.1.03.01",
            financialDestinationCodeSnapshot: "BANCO-001",
            financialDestinationNameSnapshot: "Cuenta corriente Pichincha",
            destinationTypeCodeSnapshot: "BANK_ACCOUNT",
            paymentMethodCode: "TRANSFER",
            amount: amount,
            currencyCode: "USD",
            effectiveDate: DateOnly.FromDateTime(DateTime.UtcNow),
            UserId,
            clientRequestId ?? Guid.NewGuid(),
            payloadHash,
            externalReference: "TRX-000123"
        );

    [Fact]
    public void CreateReceived_con_datos_validos_es_REFUND_RECEIVED_sin_OriginalTransactionId()
    {
        var clientRequestId = Guid.NewGuid();
        var transaction = CreateReceived(clientRequestId: clientRequestId, payloadHash: "hash-received-001");

        transaction.TransactionTypeCode.Should().Be(RefundTransactionTypeCode.RefundReceived);
        transaction.OriginalTransactionId.Should().BeNull();
        transaction.AccountingAccountId.Should().Be(AccountingAccountId);
        transaction.Reason.Should().BeNull();
        transaction.ExternalReference.Should().Be("TRX-000123");
        transaction.ClientRequestId.Should().Be(clientRequestId);
        transaction.PayloadHash.Should().Be("hash-received-001");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateReceived_rechaza_monto_no_positivo(decimal amount)
    {
        var act = () => CreateReceived(amount);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateReceived_rechaza_destino_vacio()
    {
        var act = () =>
            SupplierCreditRefundTransaction.CreateReceived(
                TenantId,
                CompanyId,
                SupplierId,
                SupplierCreditId,
                SupplierCreditMovementId,
                Guid.Empty,
                AccountingAccountId,
                "1.1.03.01",
                "BANCO-001",
                "Cuenta corriente Pichincha",
                "BANK_ACCOUNT",
                "TRANSFER",
                150m,
                "USD",
                DateOnly.FromDateTime(DateTime.UtcNow),
                UserId,
                Guid.NewGuid(),
                "hash-received-002"
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateReceived_rechaza_ClientRequestId_vacio()
    {
        var act = () => CreateReceived(clientRequestId: Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateReceived_rechaza_PayloadHash_invalido(string? hash)
    {
        var act = () => CreateReceived(payloadHash: hash!);

        act.Should().Throw<ArgumentException>();
    }

    // ── CreateReversal — hereda destino/cuenta/moneda/importe/método (§6.4quinquies) ──

    [Fact]
    public void CreateReversal_hereda_datos_del_original_sin_volver_a_resolverlos()
    {
        var original = CreateReceived(150m);
        var reversalMovementId = Guid.NewGuid();
        var reversalClientRequestId = Guid.NewGuid();

        var reversal = SupplierCreditRefundTransaction.CreateReversal(
            original,
            reversalMovementId,
            reason: "Reembolso duplicado por error",
            effectiveDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1),
            UserId,
            reversalClientRequestId,
            "hash-reversal-001"
        );

        reversal.TransactionTypeCode.Should().Be(RefundTransactionTypeCode.RefundReversed);
        reversal.OriginalTransactionId.Should().Be(original.Id);
        reversal.FinancialDestinationId.Should().Be(original.FinancialDestinationId);
        reversal.AccountingAccountId.Should().Be(original.AccountingAccountId);
        reversal.PaymentMethodCode.Should().Be(original.PaymentMethodCode);
        reversal.Amount.Should().Be(original.Amount);
        reversal.CurrencyCode.Should().Be(original.CurrencyCode);
        reversal.ExternalReference.Should().BeNull();
        reversal.Reason.Should().Be("Reembolso duplicado por error");
        reversal.ClientRequestId.Should().Be(reversalClientRequestId);
        reversal.PayloadHash.Should().Be("hash-reversal-001");
    }

    [Fact]
    public void CreateReversal_rechaza_motivo_vacio()
    {
        var original = CreateReceived();

        var act = () =>
            SupplierCreditRefundTransaction.CreateReversal(
                original,
                Guid.NewGuid(),
                " ",
                DateOnly.FromDateTime(DateTime.UtcNow),
                UserId,
                Guid.NewGuid(),
                "hash-reversal-002"
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateReversal_rechaza_ClientRequestId_vacio()
    {
        var original = CreateReceived();

        var act = () =>
            SupplierCreditRefundTransaction.CreateReversal(
                original,
                Guid.NewGuid(),
                "Motivo",
                DateOnly.FromDateTime(DateTime.UtcNow),
                UserId,
                Guid.Empty,
                "hash-reversal-003"
            );

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateReversal_rechaza_PayloadHash_invalido(string? hash)
    {
        var original = CreateReceived();

        var act = () =>
            SupplierCreditRefundTransaction.CreateReversal(
                original,
                Guid.NewGuid(),
                "Motivo",
                DateOnly.FromDateTime(DateTime.UtcNow),
                UserId,
                Guid.NewGuid(),
                hash!
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateReversal_sobre_una_reversa_previa_lanza()
    {
        var original = CreateReceived();
        var reversal = SupplierCreditRefundTransaction.CreateReversal(
            original,
            Guid.NewGuid(),
            "Motivo",
            DateOnly.FromDateTime(DateTime.UtcNow),
            UserId,
            Guid.NewGuid(),
            "hash-reversal-004"
        );

        var act = () =>
            SupplierCreditRefundTransaction.CreateReversal(
                reversal,
                Guid.NewGuid(),
                "Segunda reversa",
                DateOnly.FromDateTime(DateTime.UtcNow),
                UserId,
                Guid.NewGuid(),
                "hash-reversal-005"
            );

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ClientRequestId_y_PayloadHash_no_exponen_mutador_publico()
    {
        typeof(SupplierCreditRefundTransaction)
            .GetProperty(nameof(SupplierCreditRefundTransaction.ClientRequestId))!
            .SetMethod!.IsPublic.Should()
            .BeFalse();
        typeof(SupplierCreditRefundTransaction)
            .GetProperty(nameof(SupplierCreditRefundTransaction.PayloadHash))!
            .SetMethod!.IsPublic.Should()
            .BeFalse();
    }
}
