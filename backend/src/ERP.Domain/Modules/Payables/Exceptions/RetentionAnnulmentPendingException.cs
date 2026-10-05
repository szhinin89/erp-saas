using ERP.Domain.Exceptions;

namespace ERP.Domain.Modules.Payables.Exceptions;

/// <summary>
/// ZH-RETENTION-SRI-ANNULMENT-01 (ADR-036 O-13) — la cuenta por pagar está retenida por una solicitud
/// de anulación de su retención ante el SRI: no admite pagos, créditos ni ajustes que harían imposible
/// completar la anulación del documento origen cuando el SRI la confirme.
/// </summary>
public sealed class RetentionAnnulmentPendingException
    : DomainRuleViolationException,
        IApiCodedDomainRule
{
    public const string ErrorCode = "RETENTION_ANNULMENT_PENDING";

    public RetentionAnnulmentPendingException()
        : base(
            "La retención de este documento tiene una anulación en trámite ante el SRI. No se pueden aplicar pagos, créditos ni ajustes a su cuenta por pagar hasta que el SRI la resuelva."
        )
    { }

    public string ApiCode => ErrorCode;
}
