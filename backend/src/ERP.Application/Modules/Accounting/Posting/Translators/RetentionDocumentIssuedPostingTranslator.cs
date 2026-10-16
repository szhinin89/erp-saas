using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Modules.Retentions.Events;
using MediatR;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// RETENTIONS-EXPENSES-INTEGRATION-01D-2 — traduce RetentionDocumentIssuedEvent (Retentions) al
/// hecho contable DocumentIssued: asiento de RECLASIFICACIÓN de la retención (Debe CxP proveedor
/// por el total retenido; Haber Retención IVA/Renta por pagar por componente, ver
/// <see cref="ERP.Domain.Modules.Accounting.Enums.PostingAmountKind.RetentionVat"/>/<c>RetentionIncome</c>).
/// Las cuentas salen de la <c>PostingRule</c> Retentions/DocumentIssued de la empresa, sin cuentas
/// hardcodeadas; un componente en 0 no genera línea.
/// </summary>
/// <remarks>
/// ACCOUNTING-CONTINUITY-01 — la retención emitida genera su propia obligación durable,
/// independiente de la de su documento origen (Compra o Gasto), en la misma transacción que la
/// emisión. Un fallo contable deja la obligación Failed; nunca revierte la retención ni su origen.
/// </remarks>
public sealed class RetentionDocumentIssuedPostingTranslator
    : INotificationHandler<RetentionDocumentIssuedEvent>
{
    public const string SourceModuleName = "Retentions";
    public const string FactTypeName = "DocumentIssued";

    private readonly AccountingPostingRecorder _recorder;

    public RetentionDocumentIssuedPostingTranslator(AccountingPostingRecorder recorder) =>
        _recorder = recorder;

    public Task Handle(RetentionDocumentIssuedEvent e, CancellationToken ct) =>
        _recorder.RecordAsync(
            new AccountingPostingRequest(
                e.TenantId!.Value,
                e.CompanyId,
                SourceModuleName,
                FactTypeName,
                e.RetentionDocumentId,
                e.RetentionNumber,
                e.IssueDate,
                e.IssueDate,
                new AccountingPostingPayload(
                    Subtotal: 0m,
                    TotalVat: e.TotalRetainedVat,
                    TotalIce: 0m,
                    TotalDiscount: 0m,
                    GrandTotal: e.TotalRetained,
                    RetainedAmount: e.TotalRetained,
                    RetainedVatAmount: e.TotalRetainedVat,
                    RetainedIncomeAmount: e.TotalRetainedIncome
                )
            ),
            ct
        );
}
