using ERP.Domain.Audit;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Purchases;

public sealed class SupplierCreditAuditTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid SupplierCreditId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TargetPurchasePayableId = Guid.NewGuid();
    private static readonly Guid SourcePurchaseReturnId = Guid.NewGuid();
    private static readonly Guid FinancialDestinationId = Guid.NewGuid();
    private static readonly Guid AccountingAccountId = Guid.NewGuid();
    private static readonly Guid CashRegisterId = Guid.NewGuid();
    private static readonly Guid CashSessionId = Guid.NewGuid();
    private static readonly Guid CashMovementId = Guid.NewGuid();

    private static AuditActor Actor() =>
        new(TenantId, UserId, "tester", null, null, null, null, null, AuditSource.UserAction);

    [Fact]
    public void Create_para_Created_pobla_los_campos_comunes_y_BranchId_sin_MovementType()
    {
        var audit = SupplierCreditAudit.Create(
            Actor(),
            CompanyId,
            BranchId,
            SupplierCreditId,
            SupplierId,
            "Created",
            amount: 150m,
            balanceAfter: 150m,
            reason: "Crédito originado por devolución"
        );

        audit.TenantId.Should().Be(TenantId);
        audit.EntityId.Should().Be(SupplierCreditId);
        audit.UserId.Should().Be(UserId);
        audit.Action.Should().Be("Created");
        audit.CompanyId.Should().Be(CompanyId);
        audit.BranchId.Should().Be(BranchId);
        audit.SupplierId.Should().Be(SupplierId);
        audit.MovementType.Should().BeNull();
        audit.Amount.Should().Be(150m);
        audit.BalanceAfter.Should().Be(150m);
        audit.Reason.Should().Be("Crédito originado por devolución");
    }

    [Fact]
    public void Create_conserva_exactamente_el_BranchId_recibido_sin_sustituirlo_por_GuidEmpty()
    {
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();

        var auditA = SupplierCreditAudit.Create(
            Actor(),
            CompanyId,
            branchA,
            SupplierCreditId,
            SupplierId,
            "Created"
        );
        var auditB = SupplierCreditAudit.Create(
            Actor(),
            CompanyId,
            branchB,
            SupplierCreditId,
            SupplierId,
            "Created"
        );

        auditA.BranchId.Should().Be(branchA);
        auditB.BranchId.Should().Be(branchB);
        auditA.BranchId.Should().NotBe(auditB.BranchId);
        auditA.BranchId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_rechaza_BranchId_vacio()
    {
        var act = () =>
            SupplierCreditAudit.Create(
                Actor(),
                CompanyId,
                Guid.Empty,
                SupplierCreditId,
                SupplierId,
                "Created"
            );

        act.Should().Throw<ArgumentException>().WithParameterName("branchId");
    }

    [Fact]
    public void Create_rechaza_companyId_vacio()
    {
        var act = () =>
            SupplierCreditAudit.Create(
                Actor(),
                Guid.Empty,
                BranchId,
                SupplierCreditId,
                SupplierId,
                "Created"
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_para_Applied_pobla_MovementType_y_TargetPurchasePayableId()
    {
        var audit = SupplierCreditAudit.Create(
            Actor(),
            CompanyId,
            BranchId,
            SupplierCreditId,
            SupplierId,
            "Applied",
            movementType: SupplierCreditMovementType.Application,
            amount: 50m,
            balanceBefore: 150m,
            balanceAfter: 100m,
            targetPurchasePayableId: TargetPurchasePayableId
        );

        audit.MovementType.Should().Be(SupplierCreditMovementType.Application);
        audit.TargetPurchasePayableId.Should().Be(TargetPurchasePayableId);
        audit.BalanceBefore.Should().Be(150m);
        audit.BalanceAfter.Should().Be(100m);
        audit.Amount.Should().Be(50m);
    }

    [Fact]
    public void Create_para_ApplicationReversed_no_requiere_grupo_de_destino_financiero()
    {
        var audit = SupplierCreditAudit.Create(
            Actor(),
            CompanyId,
            BranchId,
            SupplierCreditId,
            SupplierId,
            "ApplicationReversed",
            movementType: SupplierCreditMovementType.ReversalOfApplication,
            amount: 50m,
            targetPurchasePayableId: TargetPurchasePayableId
        );

        audit.MovementType.Should().Be(SupplierCreditMovementType.ReversalOfApplication);
        audit.FinancialDestinationId.Should().BeNull();
        audit.PaymentMethodCode.Should().BeNull();
        audit.EffectiveDate.Should().BeNull();
    }

    [Fact]
    public void Create_para_Refunded_pobla_el_grupo_completo_de_destino_financiero()
    {
        var effectiveDate = new DateOnly(2026, 7, 15);

        var audit = SupplierCreditAudit.Create(
            Actor(),
            CompanyId,
            BranchId,
            SupplierCreditId,
            SupplierId,
            "Refunded",
            movementType: SupplierCreditMovementType.Refund,
            amount: 100m,
            balanceBefore: 100m,
            balanceAfter: 0m,
            statusBefore: "Open",
            statusAfter: "Closed",
            financialDestinationId: FinancialDestinationId,
            financialDestinationCodeSnapshot: "BANCO-001",
            destinationTypeCodeSnapshot: "BANK_ACCOUNT",
            accountingAccountId: AccountingAccountId,
            cashRegisterId: null,
            cashSessionId: null,
            cashMovementId: null,
            paymentMethodCode: "TRANSFER",
            externalReference: "REF-0001",
            effectiveDate: effectiveDate
        );

        audit.MovementType.Should().Be(SupplierCreditMovementType.Refund);
        audit.FinancialDestinationId.Should().Be(FinancialDestinationId);
        audit.FinancialDestinationCodeSnapshot.Should().Be("BANCO-001");
        audit.DestinationTypeCodeSnapshot.Should().Be("BANK_ACCOUNT");
        audit.AccountingAccountId.Should().Be(AccountingAccountId);
        audit.PaymentMethodCode.Should().Be("TRANSFER");
        audit.ExternalReference.Should().Be("REF-0001");
        audit.EffectiveDate.Should().Be(effectiveDate);
        audit.StatusBefore.Should().Be("Open");
        audit.StatusAfter.Should().Be("Closed");
        audit.TargetPurchasePayableId.Should().BeNull();
    }

    [Fact]
    public void Create_para_Refunded_con_destino_tipo_caja_pobla_CashRegisterId_CashSessionId_CashMovementId()
    {
        var audit = SupplierCreditAudit.Create(
            Actor(),
            CompanyId,
            BranchId,
            SupplierCreditId,
            SupplierId,
            "Refunded",
            movementType: SupplierCreditMovementType.Refund,
            amount: 100m,
            financialDestinationId: FinancialDestinationId,
            cashRegisterId: CashRegisterId,
            cashSessionId: CashSessionId,
            cashMovementId: CashMovementId,
            paymentMethodCode: "CASH",
            effectiveDate: new DateOnly(2026, 7, 20)
        );

        audit.CashRegisterId.Should().Be(CashRegisterId);
        audit.CashSessionId.Should().Be(CashSessionId);
        audit.CashMovementId.Should().Be(CashMovementId);
    }

    [Fact]
    public void Create_para_SourceReturnCancelled_pobla_los_campos_exigidos_por_20_1bis_y_deja_ambos_grupos_null()
    {
        var audit = SupplierCreditAudit.Create(
            Actor(),
            CompanyId,
            BranchId,
            SupplierCreditId,
            SupplierId,
            "SourceReturnCancelled",
            movementType: SupplierCreditMovementType.SourceReturnCancelled,
            amount: 150m,
            balanceBefore: 150m,
            balanceAfter: 0m,
            statusBefore: "Open",
            statusAfter: "Closed",
            sourcePurchaseReturnId: SourcePurchaseReturnId,
            reason: "Cancelación de la devolución de origen"
        );

        audit.MovementType.Should().Be(SupplierCreditMovementType.SourceReturnCancelled);
        audit.SourcePurchaseReturnId.Should().Be(SourcePurchaseReturnId);
        audit.BalanceBefore.Should().Be(150m);
        audit.Amount.Should().Be(150m);
        audit.BalanceAfter.Should().Be(0m);
        audit.StatusBefore.Should().Be("Open");
        audit.StatusAfter.Should().Be("Closed");
        audit.Reason.Should().Be("Cancelación de la devolución de origen");

        // Ambos grupos de campos (destino de aplicación y destino financiero de reembolso)
        // quedan null en SourceReturnCancelled (§20.1bis).
        audit.TargetPurchasePayableId.Should().BeNull();
        audit.FinancialDestinationId.Should().BeNull();
        audit.FinancialDestinationCodeSnapshot.Should().BeNull();
        audit.DestinationTypeCodeSnapshot.Should().BeNull();
        audit.AccountingAccountId.Should().BeNull();
        audit.CashRegisterId.Should().BeNull();
        audit.CashSessionId.Should().BeNull();
        audit.CashMovementId.Should().BeNull();
        audit.PaymentMethodCode.Should().BeNull();
        audit.ExternalReference.Should().BeNull();
        audit.EffectiveDate.Should().BeNull();
    }

    [Fact]
    public void Create_conserva_exactamente_los_identificadores_e_importes_recibidos_sin_sustituirlos()
    {
        var audit = SupplierCreditAudit.Create(
            Actor(),
            CompanyId,
            BranchId,
            SupplierCreditId,
            SupplierId,
            "Applied",
            movementType: SupplierCreditMovementType.Application,
            amount: 73.25m,
            balanceBefore: 200m,
            balanceAfter: 126.75m,
            targetPurchasePayableId: TargetPurchasePayableId
        );

        audit.EntityId.Should().Be(SupplierCreditId);
        audit.SupplierId.Should().Be(SupplierId);
        audit.TargetPurchasePayableId.Should().Be(TargetPurchasePayableId);
        audit.Amount.Should().Be(73.25m);
        audit.BalanceBefore.Should().Be(200m);
        audit.BalanceAfter.Should().Be(126.75m);
    }
}
