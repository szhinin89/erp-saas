using ERP.Application.Modules.Retentions.Services;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Interfaces;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Retentions;

/// <summary>
/// RETENTIONS-EXPENSES-INTEGRATION-01D-3 — cubre <see cref="RetentionCanceller"/> en aislamiento:
/// anula el <see cref="RetentionDocument"/> ya cargado, reversa la retención aplicada en la
/// <see cref="AccountsPayable"/> del documento origen si corresponde, y bloquea (sin reversa
/// insegura) cuando esa CxP ya tiene pagos aplicados. Nunca llama SaveChangesAsync — solo
/// mutaciones en memoria sobre entidades ya trackeadas.
/// </summary>
public sealed class RetentionCancellerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid ExpenseDocumentId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static RetentionDocument IssuedRetention(decimal retained = 4.50m)
    {
        var doc = RetentionDocument.Create(
            TenantId, CompanyId, BranchId, RetentionSourceDocumentType.ExpenseDocument,
            ExpenseDocumentId, SupplierId, Guid.NewGuid(), UserId
        );
        doc.AddLine(RetentionDocumentLine.Create(doc.Id, TenantId, RetentionTaxType.Vat, "725", "Retención IVA 725", 100m, 30m, retained));
        doc.Issue("001-001-000000001", new DateOnly(2026, 8, 27), UserId);
        doc.ClearDomainEvents();
        return doc;
    }

    private static AccountsPayable Payable(decimal grandTotal)
    {
        var payable = AccountsPayable.CreateFromOrigin(
            TenantId, CompanyId, BranchId, SupplierId,
            AccountsPayableOriginType.ExpenseDocument, ExpenseDocumentId,
            "01", "001-001-000000123",
            new DateOnly(2026, 8, 27), new DateOnly(2026, 8, 27), UserId
        );
        payable.AddInstallment(1, new DateOnly(2026, 8, 27), grandTotal);
        return payable;
    }

    private sealed class Fixture
    {
        public Mock<IAccountsPayableRepository> PayableRepo { get; } = new();

        public RetentionCanceller Canceller => new(PayableRepo.Object);

        public void SetupNoPayable() =>
            PayableRepo
                .Setup(r => r.GetByOriginAsync(
                    TenantId, CompanyId, AccountsPayableOriginType.ExpenseDocument,
                    ExpenseDocumentId, It.IsAny<CancellationToken>()
                ))
                .ReturnsAsync((AccountsPayable?)null);

        public void SetupPayable(AccountsPayable payable) =>
            PayableRepo
                .Setup(r => r.GetByOriginAsync(
                    TenantId, CompanyId, AccountsPayableOriginType.ExpenseDocument,
                    ExpenseDocumentId, It.IsAny<CancellationToken>()
                ))
                .ReturnsAsync(payable);
    }

    [Fact]
    public async Task Sin_CxP_asociada_solo_cancela_la_retencion()
    {
        var fx = new Fixture();
        fx.SetupNoPayable();
        var retention = IssuedRetention();

        var result = await fx.Canceller.CancelAsync(retention, "Motivo", UserId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        retention.Status.Should().Be(RetentionStatus.Cancelled);
    }

    [Fact]
    public async Task Con_CxP_sin_pagos_reversa_la_retencion_aplicada()
    {
        var fx = new Fixture();
        var payable = Payable(100m);
        payable.ApplyRetention(4.50m, UserId);
        fx.SetupPayable(payable);
        var retention = IssuedRetention(4.50m);

        var result = await fx.Canceller.CancelAsync(retention, "Motivo", UserId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        retention.Status.Should().Be(RetentionStatus.Cancelled);
        payable.RetainedAmount.Should().Be(0m);
        payable.OutstandingAmount.Should().Be(100m);
    }

    [Fact]
    public async Task Con_CxP_con_pagos_aplicados_bloquea_sin_reversar_ni_cancelar()
    {
        var fx = new Fixture();
        var payable = Payable(100m);
        payable.ApplyRetention(4.50m, UserId);
        payable.RegisterPayment(20m, UserId);
        fx.SetupPayable(payable);
        var retention = IssuedRetention(4.50m);

        var result = await fx.Canceller.CancelAsync(retention, "Motivo", UserId, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("pagos aplicados");
        retention.Status.Should().Be(RetentionStatus.Issued, "no debe mutarse si la reversa de CxP es insegura");
        payable.RetainedAmount.Should().Be(4.50m, "no debe reversarse ni parcial ni totalmente");
    }

    [Fact]
    public async Task Retencion_en_Draft_falla_con_el_mismo_mensaje_de_dominio()
    {
        var fx = new Fixture();
        fx.SetupNoPayable();
        var draft = RetentionDocument.Create(
            TenantId, CompanyId, BranchId, RetentionSourceDocumentType.ExpenseDocument,
            ExpenseDocumentId, SupplierId, Guid.NewGuid(), UserId
        );

        var result = await fx.Canceller.CancelAsync(draft, "Motivo", UserId, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("emitidas");
        draft.Status.Should().Be(RetentionStatus.Draft);
    }

    [Fact]
    public async Task CancelledBy_se_propaga_a_RetentionDocument_y_a_AccountsPayable()
    {
        var fx = new Fixture();
        var payable = Payable(100m);
        payable.ApplyRetention(4.50m, UserId);
        fx.SetupPayable(payable);
        var retention = IssuedRetention(4.50m);

        await fx.Canceller.CancelAsync(retention, "Motivo", UserId, CancellationToken.None);

        retention.CancelledBy.Should().Be(UserId);
    }

    // ── PURCHASES-RETENTIONS-CANCEL-05D: origen PurchaseInvoice ───────────────────────────────

    private static RetentionDocument IssuedRetentionForPurchase(Guid purchaseInvoiceId, decimal retained = 30m)
    {
        var doc = RetentionDocument.Create(
            TenantId, CompanyId, BranchId, RetentionSourceDocumentType.PurchaseInvoice,
            purchaseInvoiceId, SupplierId, Guid.NewGuid(), UserId
        );
        doc.AddLine(RetentionDocumentLine.Create(doc.Id, TenantId, RetentionTaxType.Vat, "725", "Retención IVA 725", 100m, 30m, retained));
        doc.Issue("001-001-000000005", new DateOnly(2026, 9, 3), UserId);
        doc.ClearDomainEvents();
        return doc;
    }

    private static AccountsPayable PayableForPurchase(Guid purchaseInvoiceId, decimal grandTotal)
    {
        var payable = AccountsPayable.CreateFromOrigin(
            TenantId, CompanyId, BranchId, SupplierId,
            AccountsPayableOriginType.PurchaseInvoice, purchaseInvoiceId,
            "01", "001-001-000000123",
            new DateOnly(2026, 8, 27), new DateOnly(2026, 9, 26), UserId
        );
        payable.AddInstallment(1, new DateOnly(2026, 9, 26), grandTotal);
        return payable;
    }

    [Fact]
    public async Task PurchaseInvoice_con_CxP_sin_pagos_reversa_la_retencion_aplicada()
    {
        var purchaseInvoiceId = Guid.NewGuid();
        var payableRepo = new Mock<IAccountsPayableRepository>();
        var payable = PayableForPurchase(purchaseInvoiceId, 115m);
        payable.ApplyRetention(30m, UserId);
        payableRepo
            .Setup(r => r.GetByOriginAsync(
                TenantId, CompanyId, AccountsPayableOriginType.PurchaseInvoice,
                purchaseInvoiceId, It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(payable);
        var canceller = new RetentionCanceller(payableRepo.Object);
        var retention = IssuedRetentionForPurchase(purchaseInvoiceId);

        var result = await canceller.CancelAsync(retention, "Motivo", UserId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        retention.Status.Should().Be(RetentionStatus.Cancelled);
        payable.RetainedAmount.Should().Be(0m);
        payable.OutstandingAmount.Should().Be(115m);
    }

    [Fact]
    public async Task PurchaseInvoice_con_CxP_con_pagos_bloquea_sin_reversar_ni_cancelar()
    {
        var purchaseInvoiceId = Guid.NewGuid();
        var payableRepo = new Mock<IAccountsPayableRepository>();
        var payable = PayableForPurchase(purchaseInvoiceId, 115m);
        payable.ApplyRetention(30m, UserId);
        payable.RegisterPayment(20m, UserId);
        payableRepo
            .Setup(r => r.GetByOriginAsync(
                TenantId, CompanyId, AccountsPayableOriginType.PurchaseInvoice,
                purchaseInvoiceId, It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(payable);
        var canceller = new RetentionCanceller(payableRepo.Object);
        var retention = IssuedRetentionForPurchase(purchaseInvoiceId);

        var result = await canceller.CancelAsync(retention, "Motivo", UserId, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("pagos aplicados");
        retention.Status.Should().Be(RetentionStatus.Issued);
        payable.RetainedAmount.Should().Be(30m);
    }

    /// <summary>
    /// PURCHASES-RETENTIONS-CANCEL-05D — a diferencia de <see cref="Sin_CxP_asociada_solo_cancela_la_retencion"/>
    /// (ExpenseDocument, comportamiento histórico tolerante mantenido a propósito), para
    /// PurchaseInvoice la CxP siempre existe desde que se confirmó la compra — no encontrarla es
    /// una inconsistencia de datos real, así que se rechaza en vez de anular dejando el pasivo sin
    /// reversar.
    /// </summary>
    [Fact]
    public async Task PurchaseInvoice_sin_CxP_asociada_y_con_monto_retenido_rechaza_la_anulacion()
    {
        var purchaseInvoiceId = Guid.NewGuid();
        var payableRepo = new Mock<IAccountsPayableRepository>();
        payableRepo
            .Setup(r => r.GetByOriginAsync(
                TenantId, CompanyId, AccountsPayableOriginType.PurchaseInvoice,
                purchaseInvoiceId, It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync((AccountsPayable?)null);
        var canceller = new RetentionCanceller(payableRepo.Object);
        var retention = IssuedRetentionForPurchase(purchaseInvoiceId);

        var result = await canceller.CancelAsync(retention, "Motivo", UserId, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("cuenta por pagar");
        retention.Status.Should().Be(RetentionStatus.Issued, "no debe anular dejando el pasivo sin reversar");
    }
}
