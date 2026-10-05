namespace ERP.Domain.Modules.ElectronicDocuments.ValueObjects;

/// <summary>
/// ADR-036 (D-8) — evidencia de que el SRI informó ANULADO un comprobante autorizado.
/// ZH-RETENTION-SRI-ANNULMENT-01B: la obtiene el ERP consultando el WS ConsultaComprobante (Ficha Técnica
/// v2.34 §8) — nunca la declara un usuario. <see cref="AnnulledOn"/> es la fecha (empresa) en que la
/// consulta informó ANULADO; <see cref="Reference"/> identifica esa consulta. <see cref="ConfirmedBy"/>
/// es quien disparó la verificación (<see cref="Guid.Empty"/> = proceso automático). Nunca se construye
/// sin fecha ni referencia.
/// </summary>
public sealed record ExternalAnnulmentEvidence
{
    public const int ReferenceMaxLen = 200;

    public DateOnly AnnulledOn { get; }
    public string Reference { get; }
    public Guid ConfirmedBy { get; }

    public ExternalAnnulmentEvidence(DateOnly annulledOn, string reference, Guid confirmedBy)
    {
        if (annulledOn == default)
            throw new ArgumentException(
                "La fecha de anulación del SRI es obligatoria.",
                nameof(annulledOn)
            );
        if (string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException(
                "La referencia/evidencia de la anulación del SRI es obligatoria.",
                nameof(reference)
            );
        if (reference.Trim().Length > ReferenceMaxLen)
            throw new ArgumentException(
                $"La referencia no puede superar {ReferenceMaxLen} caracteres.",
                nameof(reference)
            );
        AnnulledOn = annulledOn;
        Reference = reference.Trim();
        ConfirmedBy = confirmedBy;
    }

    public string Describe() =>
        $"ANULADO informado por el SRI (ConsultaComprobante) el {AnnulledOn:yyyy-MM-dd}. Referencia: {Reference}";
}
