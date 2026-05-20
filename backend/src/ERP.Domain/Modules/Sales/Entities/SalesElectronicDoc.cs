namespace ERP.Domain.Modules.Sales.Entities;

/// <summary>Extensión 1:1 SRI del documento de venta (tabla sales_electronic_doc).</summary>
public sealed class SalesElectronicDoc
{
    public Guid     SalesDocumentId       { get; private set; }
    public Guid     SubscriberId              { get; private set; }
    public Guid?    CompanyId             { get; private set; }
    public Guid?    EmissionPointId        { get; private set; }
    public Guid?    LegacyElectronicDocId { get; private set; }
    public string   DocTypeCode           { get; private set; } = "01";
    public string?  AccessKey             { get; private set; }
    public string?  AuthNumber            { get; private set; }
    public DateTime? AuthDate             { get; private set; }
    public string?  XmlSignedPath         { get; private set; }
    public string?  XmlAuthPath           { get; private set; }
    public string?  ErrorCode             { get; private set; }
    public string?  ErrorMessage          { get; private set; }
    public string?  BuyerIdType           { get; private set; }
    public string?  BuyerIdNumber         { get; private set; }
    public string?  BuyerName             { get; private set; }
    public string?  BuyerAddress          { get; private set; }
    public string?  BuyerEmail            { get; private set; }
    public string?  BuyerPhone              { get; private set; }

    public SalesDocument Document { get; private set; } = null!;

    private SalesElectronicDoc() { }

    public static SalesElectronicDoc CreateShell(Guid salesDocumentId, Guid subscriberId) =>
        new() { SalesDocumentId = salesDocumentId, SubscriberId = subscriberId, DocTypeCode = "01" };

    public void SetAuthorization(string authNumber, DateTime authDate, string? xmlSigned, string? xmlAuth)
    {
        AuthNumber    = authNumber.Trim();
        AuthDate      = authDate;
        XmlSignedPath = string.IsNullOrWhiteSpace(xmlSigned) ? null : xmlSigned.Trim();
        XmlAuthPath   = string.IsNullOrWhiteSpace(xmlAuth) ? null : xmlAuth.Trim();
        ErrorMessage  = null;
    }

    public void SetError(string? message) => ErrorMessage = message?.Trim();

    public void ClearError() => ErrorMessage = null;
}
