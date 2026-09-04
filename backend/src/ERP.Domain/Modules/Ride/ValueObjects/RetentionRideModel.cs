namespace ERP.Domain.Modules.Ride.ValueObjects;

/// <summary>
/// RETENTIONS-RIDE-TEMPLATE-03C — modelo neutro post-parsing del Comprobante de Retención,
/// análogo a <see cref="RideModel"/> (Factura/Nota de Crédito) pero con forma propia: el esquema
/// SRI <c>comprobanteRetencion</c> no tiene detalle comercial (líneas de producto), resumen de
/// impuestos de venta ni formas de pago — tiene un sujeto retenido, un documento sustento y líneas
/// de impuesto retenido. Forzar esa forma en <see cref="RideModel"/> habría sido una adaptación
/// con pérdida, mismo criterio ya adoptado para <c>RetentionElectronicDocumentData</c>
/// (RETENTIONS-ELECTRONIC-DOCUMENT-MODEL-03A) frente a <c>ElectronicDocumentData</c>.
///
/// Igual que <see cref="RideModel"/>: solo datos ya presentes en el XML autorizado, sin
/// comportamiento de aplicación ni dependencia del XML en sí una vez parseado.
/// </summary>
public sealed record RetentionRideModel
{
    public RetentionRideHeader Header { get; }
    public RideParty Issuer { get; }
    public RideParty SubjectWithheld { get; }
    public RetentionRideSourceDocument SourceDocument { get; }
    public IReadOnlyList<RetentionRideTaxLine> Lines { get; }

    /// <summary>Suma de <see cref="RetentionRideTaxLine.RetainedAmount"/> de <see cref="Lines"/> —
    /// un cálculo puramente visual sobre valores ya presentes en el XML (nunca vuelve a consultar
    /// <c>RetentionDocument</c> ni recalcula bases/porcentajes).</summary>
    public decimal TotalRetained { get; }
    public IReadOnlyList<RideAdditionalInfo> AdditionalInfo { get; }

    private RetentionRideModel(
        RetentionRideHeader header,
        RideParty issuer,
        RideParty subjectWithheld,
        RetentionRideSourceDocument sourceDocument,
        IReadOnlyList<RetentionRideTaxLine> lines,
        decimal totalRetained,
        IReadOnlyList<RideAdditionalInfo> additionalInfo
    )
    {
        Header = header;
        Issuer = issuer;
        SubjectWithheld = subjectWithheld;
        SourceDocument = sourceDocument;
        Lines = lines;
        TotalRetained = totalRetained;
        AdditionalInfo = additionalInfo;
    }

    public static RetentionRideModel Create(
        RetentionRideHeader header,
        RideParty issuer,
        RideParty subjectWithheld,
        RetentionRideSourceDocument sourceDocument,
        IReadOnlyList<RetentionRideTaxLine> lines,
        decimal totalRetained,
        IReadOnlyList<RideAdditionalInfo> additionalInfo
    )
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(subjectWithheld);
        ArgumentNullException.ThrowIfNull(sourceDocument);
        ArgumentNullException.ThrowIfNull(lines);
        if (totalRetained < 0)
            throw new ArgumentException(
                "El total retenido no puede ser negativo.",
                nameof(totalRetained)
            );
        ArgumentNullException.ThrowIfNull(additionalInfo);

        return new RetentionRideModel(
            header,
            issuer,
            subjectWithheld,
            sourceDocument,
            lines,
            totalRetained,
            additionalInfo
        );
    }
}
