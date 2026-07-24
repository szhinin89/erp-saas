namespace ERP.Domain.Modules.Sales.Entities;

public sealed class PaymentCardDetail
{
    public const int BrandMaxLen     = 30;
    public const int LastFourMaxLen  = 4;
    public const int BankMaxLen      = 100;
    public const int AuthCodeMaxLen  = 30;
    public const int LotMaxLen       = 20;

    public Guid    PaymentId         { get; private set; }
    public string? CardBrand         { get; private set; }
    public string? CardLastFour      { get; private set; }
    public string? BankName          { get; private set; }
    public string? AuthorizationCode { get; private set; }
    public string? LotNumber         { get; private set; }

    private PaymentCardDetail() { }

    public static PaymentCardDetail Create(
        Guid paymentId, string? cardBrand, string? lastFour,
        string? bankName, string? authorizationCode, string? lotNumber)
    {
        return new PaymentCardDetail
        {
            PaymentId         = paymentId,
            CardBrand         = cardBrand?.Trim(),
            CardLastFour      = lastFour?.Trim(),
            BankName          = bankName?.Trim(),
            AuthorizationCode = authorizationCode?.Trim(),
            LotNumber         = lotNumber?.Trim(),
        };
    }
}
