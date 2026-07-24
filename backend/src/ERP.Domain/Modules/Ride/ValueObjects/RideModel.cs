namespace ERP.Domain.Modules.Ride.ValueObjects;

/// <summary>
/// Modelo neutro post-parsing (ADR-025 §6): el único contrato entre <c>IRideXmlParser</c> y
/// <c>IRideTemplate</c>. Representa únicamente datos ya presentes en el XML autorizado — sin
/// comportamiento de aplicación, sin dependencia del XML en sí (una vez parseado, nunca se
/// vuelve a consultar) y sin dependencia de QuestPDF ni de ningún renderer.
/// </summary>
public sealed record RideModel
{
    public RideHeader Header { get; }
    public RideParty Issuer { get; }
    public RideParty Receiver { get; }
    public IReadOnlyList<RideLine> Lines { get; }
    public IReadOnlyList<RideTaxSummary> TaxSummary { get; }
    public IReadOnlyList<RidePaymentInfo> Payments { get; }
    public IReadOnlyList<RideAdditionalInfo> AdditionalInfo { get; }

    private RideModel(
        RideHeader header, RideParty issuer, RideParty receiver,
        IReadOnlyList<RideLine> lines, IReadOnlyList<RideTaxSummary> taxSummary,
        IReadOnlyList<RidePaymentInfo> payments, IReadOnlyList<RideAdditionalInfo> additionalInfo)
    {
        Header = header;
        Issuer = issuer;
        Receiver = receiver;
        Lines = lines;
        TaxSummary = taxSummary;
        Payments = payments;
        AdditionalInfo = additionalInfo;
    }

    public static RideModel Create(
        RideHeader header, RideParty issuer, RideParty receiver,
        IReadOnlyList<RideLine> lines, IReadOnlyList<RideTaxSummary> taxSummary,
        IReadOnlyList<RidePaymentInfo> payments, IReadOnlyList<RideAdditionalInfo> additionalInfo)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(taxSummary);
        ArgumentNullException.ThrowIfNull(payments);
        ArgumentNullException.ThrowIfNull(additionalInfo);

        return new RideModel(header, issuer, receiver, lines, taxSummary, payments, additionalInfo);
    }
}
