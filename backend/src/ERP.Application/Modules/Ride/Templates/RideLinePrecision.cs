namespace ERP.Application.Modules.Ride.Templates;

/// <summary>Company policy scales needed to present invoice and credit-note lines.</summary>
public sealed record RideLinePrecision(int QuantityDecimals, int SalesUnitPriceDecimals)
{
    /// <summary>
    /// Template version tied to the presentation scales: changing a company scale (or the legacy
    /// "unversioned" F2 PDFs) yields a different cache fingerprint and storage path.
    /// </summary>
    public string TemplateVersion => $"precision-1-q{QuantityDecimals}-p{SalesUnitPriceDecimals}";
}
