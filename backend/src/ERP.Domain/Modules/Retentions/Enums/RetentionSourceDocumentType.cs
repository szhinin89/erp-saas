namespace ERP.Domain.Modules.Retentions.Enums;

/// <summary>
/// Tipo de documento origen de una retención. Primera pieza real del módulo transversal
/// <c>Retentions</c> (ver <c>docs/decisions/RETENTIONS-MODULE-DESIGN-01.md</c>) — definida antes
/// que <c>RetentionDocument</c> porque ya es consumida por la subfase
/// <c>RETENTIONS-ELIGIBILITY-01</c>. La relación con el documento origen es genérica
/// (<c>SourceDocumentType</c> + <c>SourceDocumentId</c>), replicando el patrón ya probado de
/// <c>AccountsPayable.OriginType</c>/<c>OriginId</c> — nunca una FK fuerte por tipo de documento.
/// </summary>
public enum RetentionSourceDocumentType
{
    /// <summary>Gasto (<c>ExpenseDocument</c>) — primer consumidor implementado del módulo.</summary>
    ExpenseDocument = 0,

    /// <summary>
    /// Factura de compra — emite/cancela vía <c>IssueRetentionHandler</c>/<c>CancelRetentionHandler</c>
    /// (PURCHASES-RETENTIONS-BRIDGE-05B, PURCHASES-WITHHOLDING-LEGACY-REMOVAL-05E), única vía de
    /// retenciones para Compras. El preview de elegibilidad de <c>RETENTIONS-ELIGIBILITY-01</c>
    /// sigue sin implementar este origen (ver <c>RetentionEligibilityStatus.NotSupportedInThisPhase</c>);
    /// Compras arma sus líneas vía <c>retention-preview</c>/<c>CalculateRetentionQuery</c>.
    /// </summary>
    PurchaseInvoice = 1,

    /// <summary>
    /// Origen manual, reservado para un futuro sin documento ERP asociado — espeja
    /// <c>AccountsPayableOriginType.Manual</c>, ya reservado y sin uso en el código actual. Sin
    /// implementación en E1.
    /// </summary>
    Manual = 2,
}
